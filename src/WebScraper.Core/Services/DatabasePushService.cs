using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
            try
            {
                await remoteDb.Database.MigrateAsync();
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42701" || ex.SqlState == "42P07")
            {
                // 42701 = column already exists, 42P07 = table already exists
                // Remote DB has stale schema from a previous migration run.
                // Safe to reset since push will re-create all data from local SQLite.
                display.PrintWarning("Remote schema is stale — resetting and re-applying migrations...");
                Logger.Warning(ex, "Migration conflict detected, resetting remote schema");

                await remoteDb.Database.ExecuteSqlRawAsync(@"
                    DROP TABLE IF EXISTS ""ApiLinks"" CASCADE;
                    DROP TABLE IF EXISTS ""Injuries"" CASCADE;
                    DROP TABLE IF EXISTS ""TeamGameStats"" CASCADE;
                    DROP TABLE IF EXISTS ""PlayerGameStats"" CASCADE;
                    DROP TABLE IF EXISTS ""Games"" CASCADE;
                    DROP TABLE IF EXISTS ""Players"" CASCADE;
                    DROP TABLE IF EXISTS ""Venues"" CASCADE;
                    DROP TABLE IF EXISTS ""Teams"" CASCADE;
                    DROP TABLE IF EXISTS ""__EFMigrationsHistory"" CASCADE;
                ");

                await remoteDb.Database.MigrateAsync();
                display.PrintSuccess("Remote schema rebuilt successfully.");
            }

            // ── Teams (no FK dependencies) ──
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

            // Build team ID mapping: local -> remote (by abbreviation)
            var remoteTeams = await remoteDb.Teams.AsNoTracking().ToListAsync();
            var teamIdMap = new Dictionary<int, int>();
            foreach (var localTeam in localTeams)
            {
                var remoteTeam = remoteTeams.FirstOrDefault(t => t.Abbreviation == localTeam.Abbreviation);
                if (remoteTeam != null)
                    teamIdMap[localTeam.Id] = remoteTeam.Id;
            }

            // ── Franchises + TeamSeasons (needed before Games) ──
            var localFranchises = await localDb.Franchises.AsNoTracking().ToListAsync();
            var franchiseIdMap = new Dictionary<int, int>();
            if (localFranchises.Count > 0)
            {
                display.PrintInfo($"Pushing {localFranchises.Count} franchises...");
                foreach (var franchise in localFranchises)
                {
                    var existing = await remoteDb.Franchises
                        .FirstOrDefaultAsync(f => f.CanonicalAbbreviation == franchise.CanonicalAbbreviation);
                    if (existing != null)
                    {
                        existing.DisplayName = franchise.DisplayName;
                        franchiseIdMap[franchise.Id] = existing.Id;
                    }
                    else
                    {
                        var newFranchise = new Franchise
                        {
                            CanonicalAbbreviation = franchise.CanonicalAbbreviation,
                            DisplayName = franchise.DisplayName,
                        };
                        remoteDb.Franchises.Add(newFranchise);
                        await remoteDb.SaveChangesAsync();
                        franchiseIdMap[franchise.Id] = newFranchise.Id;
                    }
                }
                await remoteDb.SaveChangesAsync();
            }

            var localTeamSeasons = await localDb.TeamSeasons.AsNoTracking().ToListAsync();
            var teamSeasonIdMap = new Dictionary<int, int>();
            if (localTeamSeasons.Count > 0)
            {
                display.PrintInfo($"Pushing {localTeamSeasons.Count} team seasons...");
                foreach (var ts in localTeamSeasons)
                {
                    if (!franchiseIdMap.ContainsKey(ts.FranchiseId))
                    {
                        errors.Add($"TeamSeason {ts.Abbreviation} {ts.Season}: franchise mapping missing");
                        continue;
                    }

                    var remoteFranchiseId = franchiseIdMap[ts.FranchiseId];
                    var existing = await remoteDb.TeamSeasons
                        .FirstOrDefaultAsync(r => r.FranchiseId == remoteFranchiseId && r.Season == ts.Season);

                    if (existing != null)
                    {
                        existing.Name = ts.Name;
                        existing.Abbreviation = ts.Abbreviation;
                        existing.City = ts.City;
                        existing.Conference = ts.Conference;
                        existing.Division = ts.Division;
                        teamSeasonIdMap[ts.Id] = existing.Id;
                    }
                    else
                    {
                        var newTs = new TeamSeason
                        {
                            FranchiseId = remoteFranchiseId,
                            Season = ts.Season,
                            Name = ts.Name,
                            Abbreviation = ts.Abbreviation,
                            City = ts.City,
                            Conference = ts.Conference,
                            Division = ts.Division,
                        };
                        remoteDb.TeamSeasons.Add(newTs);
                        await remoteDb.SaveChangesAsync();
                        teamSeasonIdMap[ts.Id] = newTs.Id;
                    }
                }
                await remoteDb.SaveChangesAsync();
            }

            // ── Players (FK to Teams) ──
            var localPlayers = await localDb.Players.AsNoTracking().ToListAsync();
            var playerIdMap = new Dictionary<int, int>();
            if (localPlayers.Count > 0)
            {
                display.PrintInfo($"Pushing {localPlayers.Count} players...");

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
                        existing.EspnId = player.EspnId;
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
                            College = player.College,
                            EspnId = player.EspnId
                        };
                        remoteDb.Players.Add(newPlayer);
                        await remoteDb.SaveChangesAsync();
                        playerIdMap[player.Id] = newPlayer.Id;
                    }
                }
                await remoteDb.SaveChangesAsync();
                totalRecords += localPlayers.Count;
                display.PrintSuccess($"Players: {localPlayers.Count} pushed");
            }

            // ── Venues (no FK dependencies, needed before Games) ──
            var localVenues = await localDb.Venues.AsNoTracking().ToListAsync();
            var venueIdMap = new Dictionary<int, int>();
            if (localVenues.Count > 0)
            {
                display.PrintInfo($"Pushing {localVenues.Count} venues...");

                foreach (var venue in localVenues)
                {
                    var existing = await remoteDb.Venues
                        .FirstOrDefaultAsync(v => v.EspnId == venue.EspnId);

                    if (existing != null)
                    {
                        existing.Name = venue.Name;
                        existing.City = venue.City;
                        existing.State = venue.State;
                        existing.Country = venue.Country;
                        existing.IsGrass = venue.IsGrass;
                        existing.IsIndoor = venue.IsIndoor;
                        venueIdMap[venue.Id] = existing.Id;
                    }
                    else
                    {
                        var newVenue = new Venue
                        {
                            EspnId = venue.EspnId,
                            Name = venue.Name,
                            City = venue.City,
                            State = venue.State,
                            Country = venue.Country,
                            IsGrass = venue.IsGrass,
                            IsIndoor = venue.IsIndoor
                        };
                        remoteDb.Venues.Add(newVenue);
                        await remoteDb.SaveChangesAsync();
                        venueIdMap[venue.Id] = newVenue.Id;
                    }
                }
                await remoteDb.SaveChangesAsync();
                totalRecords += localVenues.Count;
                display.PrintSuccess($"Venues: {localVenues.Count} pushed");
            }

            // ── Games (FK to TeamSeasons, optional FK to Venues) ──
            var localGames = await localDb.Games.AsNoTracking().ToListAsync();
            var gameIdMap = new Dictionary<int, int>();
            if (localGames.Count > 0)
            {
                display.PrintInfo($"Pushing {localGames.Count} games...");

                foreach (var game in localGames)
                {
                    if (!teamSeasonIdMap.ContainsKey(game.HomeTeamSeasonId) || !teamSeasonIdMap.ContainsKey(game.AwayTeamSeasonId))
                    {
                        errors.Add($"Game {game.Season} week {game.Week}: team season ID mapping missing");
                        continue;
                    }

                    var remoteHomeId = teamSeasonIdMap[game.HomeTeamSeasonId];
                    var remoteAwayId = teamSeasonIdMap[game.AwayTeamSeasonId];
                    var gameDate = game.GameDate.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(game.GameDate, DateTimeKind.Utc)
                        : game.GameDate.ToUniversalTime();
                    int? remoteVenueId = game.VenueId.HasValue && venueIdMap.ContainsKey(game.VenueId.Value)
                        ? venueIdMap[game.VenueId.Value]
                        : null;

                    var existing = await remoteDb.Games
                        .FirstOrDefaultAsync(g => g.Season == game.Season
                            && g.SeasonType == game.SeasonType
                            && g.Week == game.Week
                            && g.HomeTeamSeasonId == remoteHomeId
                            && g.AwayTeamSeasonId == remoteAwayId);

                    if (existing != null)
                    {
                        existing.GameDate = gameDate;
                        existing.HomeScore = game.HomeScore;
                        existing.AwayScore = game.AwayScore;
                        existing.VenueId = remoteVenueId;
                        existing.Attendance = game.Attendance;
                        existing.NeutralSite = game.NeutralSite;
                        existing.EspnEventId = game.EspnEventId;
                        existing.GameStatus = game.GameStatus;
                        existing.HomeWinner = game.HomeWinner;
                        existing.HomeQ1 = game.HomeQ1;
                        existing.HomeQ2 = game.HomeQ2;
                        existing.HomeQ3 = game.HomeQ3;
                        existing.HomeQ4 = game.HomeQ4;
                        existing.HomeOT = game.HomeOT;
                        existing.AwayQ1 = game.AwayQ1;
                        existing.AwayQ2 = game.AwayQ2;
                        existing.AwayQ3 = game.AwayQ3;
                        existing.AwayQ4 = game.AwayQ4;
                        existing.AwayOT = game.AwayOT;
                        existing.BroadcastNetworks = game.BroadcastNetworks ?? existing.BroadcastNetworks;
                        gameIdMap[game.Id] = existing.Id;
                    }
                    else
                    {
                        var newGame = new Game
                        {
                            Season = game.Season,
                            SeasonType = game.SeasonType,
                            Week = game.Week,
                            GameDate = gameDate,
                            HomeTeamSeasonId = remoteHomeId,
                            AwayTeamSeasonId = remoteAwayId,
                            HomeScore = game.HomeScore,
                            AwayScore = game.AwayScore,
                            VenueId = remoteVenueId,
                            Attendance = game.Attendance,
                            NeutralSite = game.NeutralSite,
                            EspnEventId = game.EspnEventId,
                            GameStatus = game.GameStatus,
                            HomeWinner = game.HomeWinner,
                            HomeQ1 = game.HomeQ1,
                            HomeQ2 = game.HomeQ2,
                            HomeQ3 = game.HomeQ3,
                            HomeQ4 = game.HomeQ4,
                            HomeOT = game.HomeOT,
                            AwayQ1 = game.AwayQ1,
                            AwayQ2 = game.AwayQ2,
                            AwayQ3 = game.AwayQ3,
                            AwayQ4 = game.AwayQ4,
                            AwayOT = game.AwayOT,
                            BroadcastNetworks = game.BroadcastNetworks
                        };
                        remoteDb.Games.Add(newGame);
                        await remoteDb.SaveChangesAsync();
                        gameIdMap[game.Id] = newGame.Id;
                    }
                }
                await remoteDb.SaveChangesAsync();
                totalRecords += localGames.Count;
                display.PrintSuccess($"Games: {localGames.Count} pushed");
            }

            // ── PlayerGameStats (FK to Players and Games) ──
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
                        CopyAllPlayerStats(stat, existing);
                    }
                    else
                    {
                        var newStat = new PlayerGameStats
                        {
                            PlayerId = remotePlayerId,
                            GameId = remoteGameId
                        };
                        CopyAllPlayerStats(stat, newStat);
                        remoteDb.PlayerGameStats.Add(newStat);
                    }
                }
                await remoteDb.SaveChangesAsync();
                totalRecords += localStats.Count;
                display.PrintSuccess($"Stats: {localStats.Count} pushed");
            }

            // ── TeamGameStats (FK to Games and TeamSeasons) ──
            var localTeamStats = await localDb.TeamGameStats.AsNoTracking().ToListAsync();
            if (localTeamStats.Count > 0)
            {
                display.PrintInfo($"Pushing {localTeamStats.Count} team game stats...");
                foreach (var tgs in localTeamStats)
                {
                    if (!gameIdMap.ContainsKey(tgs.GameId) || !teamSeasonIdMap.ContainsKey(tgs.TeamSeasonId))
                    {
                        errors.Add($"TeamGameStats: game/team season ID mapping missing (G:{tgs.GameId} TS:{tgs.TeamSeasonId})");
                        continue;
                    }

                    var remoteGameId = gameIdMap[tgs.GameId];
                    var remoteTeamSeasonId = teamSeasonIdMap[tgs.TeamSeasonId];

                    var existing = await remoteDb.TeamGameStats
                        .FirstOrDefaultAsync(t => t.GameId == remoteGameId && t.TeamSeasonId == remoteTeamSeasonId);

                    if (existing != null)
                    {
                        CopyAllTeamGameStats(tgs, existing);
                    }
                    else
                    {
                        var newTgs = new TeamGameStats
                        {
                            GameId = remoteGameId,
                            TeamSeasonId = remoteTeamSeasonId
                        };
                        CopyAllTeamGameStats(tgs, newTgs);
                        remoteDb.TeamGameStats.Add(newTgs);
                    }
                }
                await remoteDb.SaveChangesAsync();
                totalRecords += localTeamStats.Count;
                display.PrintSuccess($"Team game stats: {localTeamStats.Count} pushed");
            }

            // ── Injuries (FK to Games, optional FK to Players) ──
            var localInjuries = await localDb.Injuries.AsNoTracking().ToListAsync();
            if (localInjuries.Count > 0)
            {
                display.PrintInfo($"Pushing {localInjuries.Count} injuries...");
                foreach (var injury in localInjuries)
                {
                    if (!gameIdMap.ContainsKey(injury.GameId))
                    {
                        errors.Add($"Injury: game ID mapping missing (G:{injury.GameId})");
                        continue;
                    }

                    var remoteGameId = gameIdMap[injury.GameId];
                    int? remotePlayerId = injury.PlayerId.HasValue && playerIdMap.ContainsKey(injury.PlayerId.Value)
                        ? playerIdMap[injury.PlayerId.Value]
                        : null;

                    var existing = await remoteDb.Injuries
                        .FirstOrDefaultAsync(i => i.GameId == remoteGameId && i.EspnAthleteId == injury.EspnAthleteId);

                    if (existing != null)
                    {
                        existing.PlayerId = remotePlayerId;
                        existing.PlayerName = injury.PlayerName;
                        existing.Status = injury.Status;
                        existing.InjuryType = injury.InjuryType;
                        existing.BodyLocation = injury.BodyLocation;
                        existing.Side = injury.Side;
                        existing.Detail = injury.Detail;
                        existing.ReturnDate = ToUtcOrNull(injury.ReturnDate);
                        existing.ReportDate = ToUtc(injury.ReportDate);
                    }
                    else
                    {
                        remoteDb.Injuries.Add(new Injury
                        {
                            GameId = remoteGameId,
                            PlayerId = remotePlayerId,
                            EspnAthleteId = injury.EspnAthleteId,
                            PlayerName = injury.PlayerName,
                            Status = injury.Status,
                            InjuryType = injury.InjuryType,
                            BodyLocation = injury.BodyLocation,
                            Side = injury.Side,
                            Detail = injury.Detail,
                            ReturnDate = ToUtcOrNull(injury.ReturnDate),
                            ReportDate = ToUtc(injury.ReportDate)
                        });
                    }
                }
                await remoteDb.SaveChangesAsync();
                totalRecords += localInjuries.Count;
                display.PrintSuccess($"Injuries: {localInjuries.Count} pushed");
            }

            // ── ApiLinks (FK to Games, optional FK to Teams) ──
            var localApiLinks = await localDb.ApiLinks.AsNoTracking().ToListAsync();
            if (localApiLinks.Count > 0)
            {
                display.PrintInfo($"Pushing {localApiLinks.Count} API links...");
                foreach (var link in localApiLinks)
                {
                    int? remoteGameId = link.GameId.HasValue && gameIdMap.ContainsKey(link.GameId.Value)
                        ? gameIdMap[link.GameId.Value]
                        : null;
                    int? remoteLinkTeamId = link.TeamId.HasValue && teamIdMap.ContainsKey(link.TeamId.Value)
                        ? teamIdMap[link.TeamId.Value]
                        : null;

                    var existing = await remoteDb.ApiLinks
                        .FirstOrDefaultAsync(a => a.Url == link.Url);

                    if (existing != null)
                    {
                        existing.EndpointType = link.EndpointType;
                        existing.RelationType = link.RelationType;
                        existing.GameId = remoteGameId;
                        existing.TeamId = remoteLinkTeamId;
                        existing.Season = link.Season;
                        existing.Week = link.Week;
                        existing.EspnEventId = link.EspnEventId;
                        existing.DiscoveredAt = ToUtc(link.DiscoveredAt);
                        existing.LastAccessedAt = ToUtcOrNull(link.LastAccessedAt);
                    }
                    else
                    {
                        remoteDb.ApiLinks.Add(new ApiLink
                        {
                            Url = link.Url,
                            EndpointType = link.EndpointType,
                            RelationType = link.RelationType,
                            GameId = remoteGameId,
                            TeamId = remoteLinkTeamId,
                            Season = link.Season,
                            Week = link.Week,
                            EspnEventId = link.EspnEventId,
                            DiscoveredAt = ToUtc(link.DiscoveredAt),
                            LastAccessedAt = ToUtcOrNull(link.LastAccessedAt)
                        });
                    }
                }
                await remoteDb.SaveChangesAsync();
                totalRecords += localApiLinks.Count;
                display.PrintSuccess($"API links: {localApiLinks.Count} pushed");
            }

            // ── Tier 1 game enrichment ──
            var localDrives = await localDb.GameDrives.AsNoTracking().ToListAsync();
            if (localDrives.Count > 0)
            {
                display.PrintInfo($"Pushing {localDrives.Count} game drives...");
                foreach (var drive in localDrives)
                {
                    if (!gameIdMap.ContainsKey(drive.GameId))
                        continue;

                    var remoteGameId = gameIdMap[drive.GameId];
                    int? remoteTeamSeasonId = drive.TeamSeasonId.HasValue && teamSeasonIdMap.ContainsKey(drive.TeamSeasonId.Value)
                        ? teamSeasonIdMap[drive.TeamSeasonId.Value]
                        : null;

                    var existing = await remoteDb.GameDrives
                        .FirstOrDefaultAsync(d => d.GameId == remoteGameId && d.EspnDriveId == drive.EspnDriveId);

                    if (existing != null)
                    {
                        existing.Sequence = drive.Sequence;
                        existing.TeamSeasonId = remoteTeamSeasonId;
                        existing.Description = drive.Description;
                        existing.StartPeriod = drive.StartPeriod;
                        existing.EndPeriod = drive.EndPeriod;
                        existing.TimeElapsed = drive.TimeElapsed;
                        existing.Yards = drive.Yards;
                        existing.OffensivePlays = drive.OffensivePlays;
                        existing.IsScore = drive.IsScore;
                        existing.Result = drive.Result;
                        existing.DisplayResult = drive.DisplayResult;
                    }
                    else
                    {
                        remoteDb.GameDrives.Add(new GameDrive
                        {
                            GameId = remoteGameId,
                            EspnDriveId = drive.EspnDriveId,
                            Sequence = drive.Sequence,
                            TeamSeasonId = remoteTeamSeasonId,
                            Description = drive.Description,
                            StartPeriod = drive.StartPeriod,
                            EndPeriod = drive.EndPeriod,
                            TimeElapsed = drive.TimeElapsed,
                            Yards = drive.Yards,
                            OffensivePlays = drive.OffensivePlays,
                            IsScore = drive.IsScore,
                            Result = drive.Result,
                            DisplayResult = drive.DisplayResult,
                            DataSource = drive.DataSource,
                            DataSourceFetchedAt = ToUtcOrNull(drive.DataSourceFetchedAt),
                            DataSourceRecordId = drive.DataSourceRecordId,
                            CreatedAt = ToUtc(drive.CreatedAt),
                            UpdatedAt = ToUtc(drive.UpdatedAt),
                        });
                    }
                }
                await remoteDb.SaveChangesAsync();
                totalRecords += localDrives.Count;
                display.PrintSuccess($"Game drives: {localDrives.Count} pushed");
            }

            var localScoringPlays = await localDb.ScoringPlays.AsNoTracking().ToListAsync();
            if (localScoringPlays.Count > 0)
            {
                display.PrintInfo($"Pushing {localScoringPlays.Count} scoring plays...");
                foreach (var play in localScoringPlays)
                {
                    if (!gameIdMap.ContainsKey(play.GameId))
                        continue;

                    var remoteGameId = gameIdMap[play.GameId];
                    int? remoteTeamSeasonId = play.TeamSeasonId.HasValue && teamSeasonIdMap.ContainsKey(play.TeamSeasonId.Value)
                        ? teamSeasonIdMap[play.TeamSeasonId.Value]
                        : null;

                    var existing = await remoteDb.ScoringPlays
                        .FirstOrDefaultAsync(p => p.GameId == remoteGameId && p.EspnPlayId == play.EspnPlayId);

                    if (existing != null)
                    {
                        existing.Sequence = play.Sequence;
                        existing.TeamSeasonId = remoteTeamSeasonId;
                        existing.Period = play.Period;
                        existing.Clock = play.Clock;
                        existing.PlayType = play.PlayType;
                        existing.Description = play.Description;
                        existing.HomeScore = play.HomeScore;
                        existing.AwayScore = play.AwayScore;
                        existing.ScoringType = play.ScoringType;
                    }
                    else
                    {
                        remoteDb.ScoringPlays.Add(new ScoringPlay
                        {
                            GameId = remoteGameId,
                            EspnPlayId = play.EspnPlayId,
                            Sequence = play.Sequence,
                            TeamSeasonId = remoteTeamSeasonId,
                            Period = play.Period,
                            Clock = play.Clock,
                            PlayType = play.PlayType,
                            Description = play.Description,
                            HomeScore = play.HomeScore,
                            AwayScore = play.AwayScore,
                            ScoringType = play.ScoringType,
                            DataSource = play.DataSource,
                            DataSourceFetchedAt = ToUtcOrNull(play.DataSourceFetchedAt),
                            DataSourceRecordId = play.DataSourceRecordId,
                            CreatedAt = ToUtc(play.CreatedAt),
                            UpdatedAt = ToUtc(play.UpdatedAt),
                        });
                    }
                }
                await remoteDb.SaveChangesAsync();
                totalRecords += localScoringPlays.Count;
                display.PrintSuccess($"Scoring plays: {localScoringPlays.Count} pushed");
            }

            var localWeather = await localDb.GameWeathers.AsNoTracking().ToListAsync();
            if (localWeather.Count > 0)
            {
                display.PrintInfo($"Pushing {localWeather.Count} game weather records...");
                foreach (var weather in localWeather)
                {
                    if (!gameIdMap.ContainsKey(weather.GameId))
                        continue;

                    var remoteGameId = gameIdMap[weather.GameId];
                    var existing = await remoteDb.GameWeathers.FirstOrDefaultAsync(w => w.GameId == remoteGameId);

                    if (existing != null)
                    {
                        existing.TemperatureF = weather.TemperatureF;
                        existing.HighTemperatureF = weather.HighTemperatureF;
                        existing.Condition = weather.Condition;
                        existing.WindSpeedMph = weather.WindSpeedMph;
                        existing.WindDirection = weather.WindDirection;
                        existing.HumidityPercent = weather.HumidityPercent;
                    }
                    else
                    {
                        remoteDb.GameWeathers.Add(new GameWeather
                        {
                            GameId = remoteGameId,
                            TemperatureF = weather.TemperatureF,
                            HighTemperatureF = weather.HighTemperatureF,
                            Condition = weather.Condition,
                            WindSpeedMph = weather.WindSpeedMph,
                            WindDirection = weather.WindDirection,
                            HumidityPercent = weather.HumidityPercent,
                            DataSource = weather.DataSource,
                            DataSourceFetchedAt = ToUtcOrNull(weather.DataSourceFetchedAt),
                            CreatedAt = ToUtc(weather.CreatedAt),
                            UpdatedAt = ToUtc(weather.UpdatedAt),
                        });
                    }
                }
                await remoteDb.SaveChangesAsync();
                totalRecords += localWeather.Count;
                display.PrintSuccess($"Game weather: {localWeather.Count} pushed");
            }

            var localOfficials = await localDb.GameOfficials.AsNoTracking().ToListAsync();
            if (localOfficials.Count > 0)
            {
                display.PrintInfo($"Pushing {localOfficials.Count} game officials...");
                foreach (var official in localOfficials)
                {
                    if (!gameIdMap.ContainsKey(official.GameId))
                        continue;

                    var remoteGameId = gameIdMap[official.GameId];
                    var existing = await remoteDb.GameOfficials
                        .FirstOrDefaultAsync(o =>
                            o.GameId == remoteGameId &&
                            o.Name == official.Name &&
                            o.Position == official.Position);

                    if (existing != null)
                    {
                        existing.SortOrder = official.SortOrder;
                    }
                    else
                    {
                        remoteDb.GameOfficials.Add(new GameOfficial
                        {
                            GameId = remoteGameId,
                            Name = official.Name,
                            Position = official.Position,
                            SortOrder = official.SortOrder,
                            DataSource = official.DataSource,
                            DataSourceFetchedAt = ToUtcOrNull(official.DataSourceFetchedAt),
                            CreatedAt = ToUtc(official.CreatedAt),
                            UpdatedAt = ToUtc(official.UpdatedAt),
                        });
                    }
                }
                await remoteDb.SaveChangesAsync();
                totalRecords += localOfficials.Count;
                display.PrintSuccess($"Game officials: {localOfficials.Count} pushed");
            }

            var localOdds = await localDb.GameOdds.AsNoTracking().ToListAsync();
            if (localOdds.Count > 0)
            {
                display.PrintInfo($"Pushing {localOdds.Count} game odds snapshots...");
                foreach (var odds in localOdds)
                {
                    if (!gameIdMap.ContainsKey(odds.GameId))
                        continue;

                    var remoteGameId = gameIdMap[odds.GameId];
                    var capturedAt = ToUtc(odds.CapturedAt);
                    var exists = await remoteDb.GameOdds.AnyAsync(o =>
                        o.GameId == remoteGameId &&
                        o.Sportsbook == odds.Sportsbook &&
                        o.SnapshotType == odds.SnapshotType &&
                        o.CapturedAt == capturedAt);

                    if (!exists)
                    {
                        remoteDb.GameOdds.Add(new GameOdds
                        {
                            GameId = remoteGameId,
                            Sportsbook = odds.Sportsbook,
                            Spread = odds.Spread,
                            OverUnder = odds.OverUnder,
                            HomeMoneyline = odds.HomeMoneyline,
                            AwayMoneyline = odds.AwayMoneyline,
                            SnapshotType = odds.SnapshotType,
                            CapturedAt = capturedAt,
                            Details = odds.Details,
                            DataSource = odds.DataSource,
                            DataSourceFetchedAt = ToUtcOrNull(odds.DataSourceFetchedAt),
                            CreatedAt = ToUtc(odds.CreatedAt),
                            UpdatedAt = ToUtc(odds.UpdatedAt),
                        });
                    }
                }
                await remoteDb.SaveChangesAsync();
                totalRecords += localOdds.Count;
                display.PrintSuccess($"Game odds: {localOdds.Count} pushed");
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

    private static void CopyAllPlayerStats(PlayerGameStats source, PlayerGameStats target)
    {
        // Passing
        target.PassAttempts = source.PassAttempts;
        target.PassCompletions = source.PassCompletions;
        target.PassYards = source.PassYards;
        target.PassTouchdowns = source.PassTouchdowns;
        target.Interceptions = source.Interceptions;
        target.QBRating = source.QBRating;
        target.AdjQBR = source.AdjQBR;
        target.Sacks = source.Sacks;
        target.SackYardsLost = source.SackYardsLost;

        // Rushing
        target.RushAttempts = source.RushAttempts;
        target.RushYards = source.RushYards;
        target.RushTouchdowns = source.RushTouchdowns;
        target.LongRushing = source.LongRushing;

        // Receiving
        target.Receptions = source.Receptions;
        target.ReceivingYards = source.ReceivingYards;
        target.ReceivingTouchdowns = source.ReceivingTouchdowns;
        target.ReceivingTargets = source.ReceivingTargets;
        target.LongReception = source.LongReception;
        target.YardsPerReception = source.YardsPerReception;

        // Fumbles
        target.Fumbles = source.Fumbles;
        target.FumblesLost = source.FumblesLost;
        target.FumblesRecovered = source.FumblesRecovered;

        // Defensive
        target.TotalTackles = source.TotalTackles;
        target.SoloTackles = source.SoloTackles;
        target.DefensiveSacks = source.DefensiveSacks;
        target.TacklesForLoss = source.TacklesForLoss;
        target.PassesDefended = source.PassesDefended;
        target.QBHits = source.QBHits;
        target.DefensiveTouchdowns = source.DefensiveTouchdowns;

        // Interceptions (defensive)
        target.InterceptionsCaught = source.InterceptionsCaught;
        target.InterceptionYards = source.InterceptionYards;
        target.InterceptionTouchdowns = source.InterceptionTouchdowns;

        // Kick returns
        target.KickReturns = source.KickReturns;
        target.KickReturnYards = source.KickReturnYards;
        target.LongKickReturn = source.LongKickReturn;
        target.KickReturnTouchdowns = source.KickReturnTouchdowns;

        // Punt returns
        target.PuntReturns = source.PuntReturns;
        target.PuntReturnYards = source.PuntReturnYards;
        target.LongPuntReturn = source.LongPuntReturn;
        target.PuntReturnTouchdowns = source.PuntReturnTouchdowns;

        // Kicking
        target.FieldGoalsMade = source.FieldGoalsMade;
        target.FieldGoalAttempts = source.FieldGoalAttempts;
        target.LongFieldGoal = source.LongFieldGoal;
        target.ExtraPointsMade = source.ExtraPointsMade;
        target.ExtraPointAttempts = source.ExtraPointAttempts;
        target.TotalKickingPoints = source.TotalKickingPoints;

        // Punting
        target.Punts = source.Punts;
        target.PuntYards = source.PuntYards;
        target.GrossAvgPuntYards = source.GrossAvgPuntYards;
        target.PuntTouchbacks = source.PuntTouchbacks;
        target.PuntsInside20 = source.PuntsInside20;
        target.LongPunt = source.LongPunt;
    }

    private static void CopyAllTeamGameStats(TeamGameStats source, TeamGameStats target)
    {
        target.FirstDowns = source.FirstDowns;
        target.FirstDownsPassing = source.FirstDownsPassing;
        target.FirstDownsRushing = source.FirstDownsRushing;
        target.FirstDownsPenalty = source.FirstDownsPenalty;
        target.ThirdDownMade = source.ThirdDownMade;
        target.ThirdDownAttempts = source.ThirdDownAttempts;
        target.FourthDownMade = source.FourthDownMade;
        target.FourthDownAttempts = source.FourthDownAttempts;
        target.TotalPlays = source.TotalPlays;
        target.TotalYards = source.TotalYards;
        target.NetPassingYards = source.NetPassingYards;
        target.PassCompletions = source.PassCompletions;
        target.PassAttempts = source.PassAttempts;
        target.YardsPerPass = source.YardsPerPass;
        target.InterceptionsThrown = source.InterceptionsThrown;
        target.SacksAgainst = source.SacksAgainst;
        target.SackYardsLost = source.SackYardsLost;
        target.RushingYards = source.RushingYards;
        target.RushingAttempts = source.RushingAttempts;
        target.YardsPerRush = source.YardsPerRush;
        target.RedZoneMade = source.RedZoneMade;
        target.RedZoneAttempts = source.RedZoneAttempts;
        target.Turnovers = source.Turnovers;
        target.FumblesLost = source.FumblesLost;
        target.Penalties = source.Penalties;
        target.PenaltyYards = source.PenaltyYards;
        target.DefensiveTouchdowns = source.DefensiveTouchdowns;
        target.PossessionTime = source.PossessionTime;
    }

    private static DateTime ToUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();

    private static DateTime? ToUtcOrNull(DateTime? dt) =>
        dt.HasValue ? ToUtc(dt.Value) : null;
}
