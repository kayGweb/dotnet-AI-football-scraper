using Microsoft.EntityFrameworkCore;
using Serilog;
using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Services;

public class DatabasePushService
{
    private static readonly ILogger Logger = Log.ForContext<DatabasePushService>();

    /// <summary>
    /// Pushes all data from the local SQLite database to a remote PostgreSQL database.
    /// Reads the PostgreSQL connection string from configuration (ConnectionStrings:PostgreSQL).
    /// </summary>
    public async Task<ScrapeResult> PushToServerAsync(
        AppDbContext localDb,
        string postgresConnectionString,
        ConsoleDisplayService display)
    {
        var errors = new List<string>();
        int totalRecords = 0;

        try
        {
            display.PrintInfo("Pushing local SQLite data to remote PostgreSQL...");
            Console.WriteLine();

            // Build a separate DbContext for the remote PostgreSQL database
            var pgOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(postgresConnectionString)
                .Options;

            using var remoteDb = new AppDbContext(pgOptions);

            // Ensure remote schema is up to date
            display.PrintInfo("Applying migrations to remote database...");
            await remoteDb.Database.MigrateAsync();

            // Push Teams first (no FK dependencies)
            var localTeams = await localDb.Teams.AsNoTracking().ToListAsync();
            if (localTeams.Count > 0)
            {
                display.PrintInfo($"Pushing {localTeams.Count} teams...");
                foreach (var team in localTeams)
                {
                    var existing = await remoteDb.Teams
                        .FirstOrDefaultAsync(t => t.Abbreviation == team.Abbreviation);

                    if (existing != null)
                    {
                        existing.Name = team.Name;
                        existing.City = team.City;
                        existing.Conference = team.Conference;
                        existing.Division = team.Division;
                    }
                    else
                    {
                        remoteDb.Teams.Add(new Team
                        {
                            Name = team.Name,
                            Abbreviation = team.Abbreviation,
                            City = team.City,
                            Conference = team.Conference,
                            Division = team.Division
                        });
                    }
                }
                await remoteDb.SaveChangesAsync();
                totalRecords += localTeams.Count;
                display.PrintSuccess($"Teams: {localTeams.Count} pushed");
            }
            else
            {
                display.PrintWarning("No teams in local database to push.");
            }

            // Build ID mapping: local team ID -> remote team ID (by abbreviation)
            var remoteTeams = await remoteDb.Teams.AsNoTracking().ToListAsync();
            var teamIdMap = new Dictionary<int, int>();
            foreach (var localTeam in localTeams)
            {
                var remoteTeam = remoteTeams.FirstOrDefault(t => t.Abbreviation == localTeam.Abbreviation);
                if (remoteTeam != null)
                    teamIdMap[localTeam.Id] = remoteTeam.Id;
            }

            // Push Players (FK to Teams)
            var localPlayers = await localDb.Players.AsNoTracking().ToListAsync();
            if (localPlayers.Count > 0)
            {
                display.PrintInfo($"Pushing {localPlayers.Count} players...");
                var playerIdMap = new Dictionary<int, int>();

                foreach (var player in localPlayers)
                {
                    int? remoteTeamId = player.TeamId.HasValue && teamIdMap.ContainsKey(player.TeamId.Value)
                        ? teamIdMap[player.TeamId.Value]
                        : null;

                    var existing = await remoteDb.Players
                        .FirstOrDefaultAsync(p => p.Name == player.Name && p.TeamId == remoteTeamId);

                    if (existing != null)
                    {
                        existing.Position = player.Position;
                        existing.JerseyNumber = player.JerseyNumber;
                        existing.Height = player.Height;
                        existing.Weight = player.Weight;
                        existing.College = player.College;
                        playerIdMap[player.Id] = existing.Id;
                    }
                    else
                    {
                        var newPlayer = new Player
                        {
                            Name = player.Name,
                            TeamId = remoteTeamId,
                            Position = player.Position,
                            JerseyNumber = player.JerseyNumber,
                            Height = player.Height,
                            Weight = player.Weight,
                            College = player.College
                        };
                        remoteDb.Players.Add(newPlayer);
                        await remoteDb.SaveChangesAsync();
                        playerIdMap[player.Id] = newPlayer.Id;
                    }
                }
                await remoteDb.SaveChangesAsync();
                totalRecords += localPlayers.Count;
                display.PrintSuccess($"Players: {localPlayers.Count} pushed");

                // Push Games (FK to Teams)
                var localGames = await localDb.Games.AsNoTracking().ToListAsync();
                if (localGames.Count > 0)
                {
                    display.PrintInfo($"Pushing {localGames.Count} games...");
                    var gameIdMap = new Dictionary<int, int>();

                    foreach (var game in localGames)
                    {
                        if (!teamIdMap.ContainsKey(game.HomeTeamId) || !teamIdMap.ContainsKey(game.AwayTeamId))
                        {
                            errors.Add($"Game {game.Season} week {game.Week}: team ID mapping missing");
                            continue;
                        }

                        var remoteHomeId = teamIdMap[game.HomeTeamId];
                        var remoteAwayId = teamIdMap[game.AwayTeamId];
                        var gameDate = game.GameDate.Kind == DateTimeKind.Unspecified
                            ? DateTime.SpecifyKind(game.GameDate, DateTimeKind.Utc)
                            : game.GameDate.ToUniversalTime();

                        var existing = await remoteDb.Games
                            .FirstOrDefaultAsync(g => g.Season == game.Season
                                && g.Week == game.Week
                                && g.HomeTeamId == remoteHomeId
                                && g.AwayTeamId == remoteAwayId);

                        if (existing != null)
                        {
                            existing.GameDate = gameDate;
                            existing.HomeScore = game.HomeScore;
                            existing.AwayScore = game.AwayScore;
                            gameIdMap[game.Id] = existing.Id;
                        }
                        else
                        {
                            var newGame = new Game
                            {
                                Season = game.Season,
                                Week = game.Week,
                                GameDate = gameDate,
                                HomeTeamId = remoteHomeId,
                                AwayTeamId = remoteAwayId,
                                HomeScore = game.HomeScore,
                                AwayScore = game.AwayScore
                            };
                            remoteDb.Games.Add(newGame);
                            await remoteDb.SaveChangesAsync();
                            gameIdMap[game.Id] = newGame.Id;
                        }
                    }
                    await remoteDb.SaveChangesAsync();
                    totalRecords += localGames.Count;
                    display.PrintSuccess($"Games: {localGames.Count} pushed");

                    // Push PlayerGameStats (FK to Players and Games)
                    var localStats = await localDb.PlayerGameStats.AsNoTracking().ToListAsync();
                    if (localStats.Count > 0)
                    {
                        display.PrintInfo($"Pushing {localStats.Count} player stats...");
                        foreach (var stat in localStats)
                        {
                            if (!playerIdMap.ContainsKey(stat.PlayerId) || !gameIdMap.ContainsKey(stat.GameId))
                            {
                                errors.Add($"Stat record: player/game ID mapping missing (P:{stat.PlayerId} G:{stat.GameId})");
                                continue;
                            }

                            var remotePlayerId = playerIdMap[stat.PlayerId];
                            var remoteGameId = gameIdMap[stat.GameId];

                            var existing = await remoteDb.PlayerGameStats
                                .FirstOrDefaultAsync(s => s.PlayerId == remotePlayerId && s.GameId == remoteGameId);

                            if (existing != null)
                            {
                                existing.PassAttempts = stat.PassAttempts;
                                existing.PassCompletions = stat.PassCompletions;
                                existing.PassYards = stat.PassYards;
                                existing.PassTouchdowns = stat.PassTouchdowns;
                                existing.Interceptions = stat.Interceptions;
                                existing.RushAttempts = stat.RushAttempts;
                                existing.RushYards = stat.RushYards;
                                existing.RushTouchdowns = stat.RushTouchdowns;
                                existing.Receptions = stat.Receptions;
                                existing.ReceivingYards = stat.ReceivingYards;
                                existing.ReceivingTouchdowns = stat.ReceivingTouchdowns;
                            }
                            else
                            {
                                remoteDb.PlayerGameStats.Add(new PlayerGameStats
                                {
                                    PlayerId = remotePlayerId,
                                    GameId = remoteGameId,
                                    PassAttempts = stat.PassAttempts,
                                    PassCompletions = stat.PassCompletions,
                                    PassYards = stat.PassYards,
                                    PassTouchdowns = stat.PassTouchdowns,
                                    Interceptions = stat.Interceptions,
                                    RushAttempts = stat.RushAttempts,
                                    RushYards = stat.RushYards,
                                    RushTouchdowns = stat.RushTouchdowns,
                                    Receptions = stat.Receptions,
                                    ReceivingYards = stat.ReceivingYards,
                                    ReceivingTouchdowns = stat.ReceivingTouchdowns
                                });
                            }
                        }
                        await remoteDb.SaveChangesAsync();
                        totalRecords += localStats.Count;
                        display.PrintSuccess($"Stats: {localStats.Count} pushed");
                    }
                }
            }

            Console.WriteLine();

            if (errors.Count > 0)
            {
                display.PrintWarning($"Push completed with {errors.Count} warnings. {totalRecords} records pushed.");
                Logger.Warning("Push completed with {ErrorCount} warnings: {@Errors}", errors.Count, errors);
                return new ScrapeResult
                {
                    Success = true,
                    RecordsProcessed = totalRecords,
                    Message = $"Push completed with {errors.Count} warnings. {totalRecords} records pushed.",
                    Errors = errors
                };
            }

            return ScrapeResult.Succeeded(totalRecords, $"Successfully pushed {totalRecords} records to PostgreSQL");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Push to PostgreSQL failed");
            return ScrapeResult.Failed($"Push failed: {ex.Message}");
        }
    }
}
