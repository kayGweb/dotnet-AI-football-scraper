using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Services;

/// <summary>
/// Reads all data from the local SQLite database and pushes it to a remote PostgreSQL database.
/// Uses a separate DbContext pointed at the PostgreSQL connection string ("ConnectionStrings:PostgreSQL").
/// </summary>
public class DatabasePushService
{
    private static readonly ILogger Logger = Log.ForContext<DatabasePushService>();

    private readonly AppDbContext _localContext;
    private readonly IConfiguration _configuration;

    public DatabasePushService(AppDbContext localContext, IConfiguration configuration)
    {
        _localContext = localContext;
        _configuration = configuration;
    }

    public async Task<ScrapeResult> PushAsync()
    {
        var remoteConnString = _configuration.GetConnectionString("PostgreSQL");
        if (string.IsNullOrWhiteSpace(remoteConnString))
        {
            return ScrapeResult.Failed(
                "No PostgreSQL connection string configured. " +
                "Add \"ConnectionStrings:PostgreSQL\" to appsettings.Local.json.");
        }

        if (remoteConnString.Contains("YOUR_PASSWORD_HERE", StringComparison.Ordinal))
        {
            return ScrapeResult.Failed(
                "PostgreSQL connection string still contains placeholder password. " +
                "Update the password in appsettings.Local.json.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(remoteConnString)
            .Options;

        await using var remoteContext = new AppDbContext(options);

        try
        {
            await remoteContext.Database.MigrateAsync();
            Logger.Information("Remote PostgreSQL database migrated");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to connect/migrate remote PostgreSQL database");
            return ScrapeResult.Failed($"Could not connect to remote database: {ex.Message}");
        }

        int totalPushed = 0;
        var errors = new List<string>();

        // 1. Push teams (no FK dependencies)
        var teams = await _localContext.Teams.AsNoTracking().ToListAsync();
        if (teams.Count > 0)
        {
            var (pushed, err) = await PushTeamsAsync(remoteContext, teams);
            totalPushed += pushed;
            if (err != null) errors.Add(err);
        }

        // 2. Push players (FK -> Team via Abbreviation lookup)
        var players = await _localContext.Players
            .AsNoTracking()
            .Include(p => p.Team)
            .ToListAsync();
        if (players.Count > 0)
        {
            var (pushed, err) = await PushPlayersAsync(remoteContext, players);
            totalPushed += pushed;
            if (err != null) errors.Add(err);
        }

        // 3. Push games (FK -> HomeTeam, AwayTeam via Abbreviation lookup)
        var games = await _localContext.Games
            .AsNoTracking()
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .ToListAsync();
        if (games.Count > 0)
        {
            var (pushed, err) = await PushGamesAsync(remoteContext, games);
            totalPushed += pushed;
            if (err != null) errors.Add(err);
        }

        // 4. Push stats (FK -> Player via Name, Game via season+week+teams)
        var stats = await _localContext.PlayerGameStats
            .AsNoTracking()
            .Include(s => s.Player)
            .Include(s => s.Game)
                .ThenInclude(g => g.HomeTeam)
            .Include(s => s.Game)
                .ThenInclude(g => g.AwayTeam)
            .ToListAsync();
        if (stats.Count > 0)
        {
            var (pushed, err) = await PushStatsAsync(remoteContext, stats);
            totalPushed += pushed;
            if (err != null) errors.Add(err);
        }

        if (errors.Count > 0)
        {
            return new ScrapeResult
            {
                Success = false,
                RecordsProcessed = totalPushed,
                Message = $"Push completed with errors. {totalPushed} records pushed.",
                Errors = errors
            };
        }

        return ScrapeResult.Succeeded(totalPushed,
            $"All data pushed to remote PostgreSQL ({teams.Count} teams, {players.Count} players, " +
            $"{games.Count} games, {stats.Count} stat lines)");
    }

    private static async Task<(int pushed, string? error)> PushTeamsAsync(
        AppDbContext remote, List<Team> teams)
    {
        try
        {
            foreach (var team in teams)
            {
                var existing = await remote.Teams
                    .FirstOrDefaultAsync(t => t.Abbreviation == team.Abbreviation);

                if (existing != null)
                {
                    existing.Name = team.Name;
                    existing.City = team.City;
                    existing.Conference = team.Conference;
                    existing.Division = team.Division;
                    remote.Teams.Update(existing);
                }
                else
                {
                    remote.Teams.Add(new Team
                    {
                        Name = team.Name,
                        Abbreviation = team.Abbreviation,
                        City = team.City,
                        Conference = team.Conference,
                        Division = team.Division
                    });
                }
            }

            await remote.SaveChangesAsync();
            Logger.Information("Pushed {Count} teams", teams.Count);
            return (teams.Count, null);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to push teams");
            return (0, $"Teams push failed: {ex.Message}");
        }
    }

    private static async Task<(int pushed, string? error)> PushPlayersAsync(
        AppDbContext remote, List<Player> players)
    {
        try
        {
            var remoteTeamMap = await remote.Teams
                .AsNoTracking()
                .ToDictionaryAsync(t => t.Abbreviation, t => t.Id);

            foreach (var player in players)
            {
                int? remoteTeamId = null;
                if (player.Team != null && remoteTeamMap.TryGetValue(player.Team.Abbreviation, out var tid))
                    remoteTeamId = tid;

                var existing = await remote.Players
                    .FirstOrDefaultAsync(p => p.Name == player.Name && p.TeamId == remoteTeamId);

                if (existing != null)
                {
                    existing.Position = player.Position;
                    existing.JerseyNumber = player.JerseyNumber;
                    existing.Height = player.Height;
                    existing.Weight = player.Weight;
                    existing.College = player.College;
                    remote.Players.Update(existing);
                }
                else
                {
                    remote.Players.Add(new Player
                    {
                        Name = player.Name,
                        TeamId = remoteTeamId,
                        Position = player.Position,
                        JerseyNumber = player.JerseyNumber,
                        Height = player.Height,
                        Weight = player.Weight,
                        College = player.College
                    });
                }
            }

            await remote.SaveChangesAsync();
            Logger.Information("Pushed {Count} players", players.Count);
            return (players.Count, null);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to push players");
            return (0, $"Players push failed: {ex.Message}");
        }
    }

    private static async Task<(int pushed, string? error)> PushGamesAsync(
        AppDbContext remote, List<Game> games)
    {
        try
        {
            var remoteTeamMap = await remote.Teams
                .AsNoTracking()
                .ToDictionaryAsync(t => t.Abbreviation, t => t.Id);

            foreach (var game in games)
            {
                var homeAbbr = game.HomeTeam?.Abbreviation;
                var awayAbbr = game.AwayTeam?.Abbreviation;

                if (homeAbbr == null || awayAbbr == null ||
                    !remoteTeamMap.TryGetValue(homeAbbr, out var remoteHomeId) ||
                    !remoteTeamMap.TryGetValue(awayAbbr, out var remoteAwayId))
                {
                    Logger.Warning("Skipping game {Season} W{Week} — team not found on remote",
                        game.Season, game.Week);
                    continue;
                }

                var existing = await remote.Games
                    .FirstOrDefaultAsync(g =>
                        g.Season == game.Season &&
                        g.Week == game.Week &&
                        g.HomeTeamId == remoteHomeId &&
                        g.AwayTeamId == remoteAwayId);

                if (existing != null)
                {
                    existing.GameDate = game.GameDate;
                    existing.HomeScore = game.HomeScore;
                    existing.AwayScore = game.AwayScore;
                    remote.Games.Update(existing);
                }
                else
                {
                    remote.Games.Add(new Game
                    {
                        Season = game.Season,
                        Week = game.Week,
                        GameDate = game.GameDate,
                        HomeTeamId = remoteHomeId,
                        AwayTeamId = remoteAwayId,
                        HomeScore = game.HomeScore,
                        AwayScore = game.AwayScore
                    });
                }
            }

            await remote.SaveChangesAsync();
            Logger.Information("Pushed {Count} games", games.Count);
            return (games.Count, null);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to push games");
            return (0, $"Games push failed: {ex.Message}");
        }
    }

    private static async Task<(int pushed, string? error)> PushStatsAsync(
        AppDbContext remote, List<PlayerGameStats> stats)
    {
        try
        {
            var remoteTeamMap = await remote.Teams
                .AsNoTracking()
                .ToDictionaryAsync(t => t.Abbreviation, t => t.Id);

            var remotePlayerCache = new Dictionary<string, int>();
            int pushed = 0;

            foreach (var stat in stats)
            {
                var playerName = stat.Player?.Name;
                if (playerName == null) continue;

                // Resolve remote player ID (cache lookups to avoid repeated queries)
                if (!remotePlayerCache.TryGetValue(playerName, out var remotePlayerId))
                {
                    var remotePlayer = await remote.Players
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Name == playerName);
                    if (remotePlayer == null)
                    {
                        Logger.Warning("Skipping stats for {Player} — not found on remote", playerName);
                        continue;
                    }
                    remotePlayerId = remotePlayer.Id;
                    remotePlayerCache[playerName] = remotePlayerId;
                }

                // Resolve remote game ID
                var homeAbbr = stat.Game?.HomeTeam?.Abbreviation;
                var awayAbbr = stat.Game?.AwayTeam?.Abbreviation;
                if (stat.Game == null || homeAbbr == null || awayAbbr == null ||
                    !remoteTeamMap.TryGetValue(homeAbbr, out var rHome) ||
                    !remoteTeamMap.TryGetValue(awayAbbr, out var rAway))
                    continue;

                var remoteGame = await remote.Games
                    .AsNoTracking()
                    .FirstOrDefaultAsync(g =>
                        g.Season == stat.Game.Season &&
                        g.Week == stat.Game.Week &&
                        g.HomeTeamId == rHome &&
                        g.AwayTeamId == rAway);

                if (remoteGame == null)
                {
                    Logger.Warning("Skipping stats for {Player} — game not found on remote", playerName);
                    continue;
                }

                var existing = await remote.PlayerGameStats
                    .FirstOrDefaultAsync(s => s.PlayerId == remotePlayerId && s.GameId == remoteGame.Id);

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
                    remote.PlayerGameStats.Update(existing);
                }
                else
                {
                    remote.PlayerGameStats.Add(new PlayerGameStats
                    {
                        PlayerId = remotePlayerId,
                        GameId = remoteGame.Id,
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

                pushed++;
            }

            await remote.SaveChangesAsync();
            Logger.Information("Pushed {Count} stat lines", pushed);
            return (pushed, null);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to push stats");
            return (0, $"Stats push failed: {ex.Message}");
        }
    }
}
