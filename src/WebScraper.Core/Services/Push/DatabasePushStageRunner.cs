using Microsoft.EntityFrameworkCore;
using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Services.Push;

internal static class DatabasePushStageRunner
{
    public static async Task EnsureRemoteSchemaAsync(
        AppDbContext remoteDb,
        ConsoleDisplayService display,
        CancellationToken cancellationToken = default)
    {
        display.PrintInfo("Applying migrations to remote database...");
        try
        {
            await remoteDb.Database.MigrateAsync(cancellationToken);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState is "42701" or "42P07")
        {
            display.PrintWarning("Remote schema is stale — resetting and re-applying migrations...");
            await remoteDb.Database.ExecuteSqlRawAsync("""
                DROP TABLE IF EXISTS "GameOdds" CASCADE;
                DROP TABLE IF EXISTS "GameOfficials" CASCADE;
                DROP TABLE IF EXISTS "GameWeathers" CASCADE;
                DROP TABLE IF EXISTS "ScoringPlays" CASCADE;
                DROP TABLE IF EXISTS "GameDrives" CASCADE;
                DROP TABLE IF EXISTS "ApiLinks" CASCADE;
                DROP TABLE IF EXISTS "Injuries" CASCADE;
                DROP TABLE IF EXISTS "TeamGameStats" CASCADE;
                DROP TABLE IF EXISTS "PlayerGameStats" CASCADE;
                DROP TABLE IF EXISTS "Games" CASCADE;
                DROP TABLE IF EXISTS "PlayerTeamSeasons" CASCADE;
                DROP TABLE IF EXISTS "TeamSeasons" CASCADE;
                DROP TABLE IF EXISTS "Franchises" CASCADE;
                DROP TABLE IF EXISTS "Players" CASCADE;
                DROP TABLE IF EXISTS "Venues" CASCADE;
                DROP TABLE IF EXISTS "Teams" CASCADE;
                DROP TABLE IF EXISTS "__EFMigrationsHistory" CASCADE;
                """, cancellationToken);
            await remoteDb.Database.MigrateAsync(cancellationToken);
            display.PrintSuccess("Remote schema rebuilt successfully.");
        }
    }

    public static async Task<int> PushTeamsAsync(PushExecutionContext ctx, CancellationToken ct)
    {
        var localTeams = await ctx.LocalDb.Teams.AsNoTracking().ToListAsync(ct);
        if (localTeams.Count == 0)
        {
            ctx.Display.PrintWarning("No teams in local database to push.");
            return 0;
        }

        ctx.Display.PrintInfo($"Pushing {localTeams.Count} teams...");
        foreach (var team in localTeams)
        {
            var existing = await ctx.RemoteDb.Teams
                .FirstOrDefaultAsync(t => t.Abbreviation == team.Abbreviation, ct);

            if (existing != null)
            {
                existing.Name = team.Name;
                existing.City = team.City;
                existing.Conference = team.Conference;
                existing.Division = team.Division;
                ctx.Maps.TeamIdMap[team.Id] = existing.Id;
            }
            else
            {
                var newTeam = new Team
                {
                    Name = team.Name,
                    Abbreviation = team.Abbreviation,
                    City = team.City,
                    Conference = team.Conference,
                    Division = team.Division,
                };
                ctx.RemoteDb.Teams.Add(newTeam);
                await ctx.RemoteDb.SaveChangesAsync(ct);
                ctx.Maps.TeamIdMap[team.Id] = newTeam.Id;
            }
        }

        await ctx.RemoteDb.SaveChangesAsync(ct);
        ctx.Display.PrintSuccess($"Teams: {localTeams.Count} pushed");
        return localTeams.Count;
    }

    public static async Task<int> PushFranchisesAsync(PushExecutionContext ctx, CancellationToken ct)
    {
        var localFranchises = await ctx.LocalDb.Franchises.AsNoTracking().ToListAsync(ct);
        if (localFranchises.Count == 0)
            return 0;

        ctx.Display.PrintInfo($"Pushing {localFranchises.Count} franchises...");
        foreach (var franchise in localFranchises)
        {
            var existing = await ctx.RemoteDb.Franchises
                .FirstOrDefaultAsync(f => f.CanonicalAbbreviation == franchise.CanonicalAbbreviation, ct);
            if (existing != null)
            {
                existing.DisplayName = franchise.DisplayName;
                ctx.Maps.FranchiseIdMap[franchise.Id] = existing.Id;
            }
            else
            {
                var newFranchise = new Franchise
                {
                    CanonicalAbbreviation = franchise.CanonicalAbbreviation,
                    DisplayName = franchise.DisplayName,
                };
                ctx.RemoteDb.Franchises.Add(newFranchise);
                await ctx.RemoteDb.SaveChangesAsync(ct);
                ctx.Maps.FranchiseIdMap[franchise.Id] = newFranchise.Id;
            }
        }

        await ctx.RemoteDb.SaveChangesAsync(ct);
        return localFranchises.Count;
    }

    public static async Task<int> PushTeamSeasonsAsync(PushExecutionContext ctx, CancellationToken ct)
    {
        var localTeamSeasons = await ctx.LocalDb.TeamSeasons.AsNoTracking().ToListAsync(ct);
        if (localTeamSeasons.Count == 0)
            return 0;

        ctx.Display.PrintInfo($"Pushing {localTeamSeasons.Count} team seasons...");
        foreach (var ts in localTeamSeasons)
        {
            if (!ctx.Maps.FranchiseIdMap.TryGetValue(ts.FranchiseId, out var remoteFranchiseId))
            {
                ctx.Errors.Add($"TeamSeason {ts.Abbreviation} {ts.Season}: franchise mapping missing");
                continue;
            }

            var existing = await ctx.RemoteDb.TeamSeasons
                .FirstOrDefaultAsync(r => r.FranchiseId == remoteFranchiseId && r.Season == ts.Season, ct);

            if (existing != null)
            {
                existing.Name = ts.Name;
                existing.Abbreviation = ts.Abbreviation;
                existing.City = ts.City;
                existing.Conference = ts.Conference;
                existing.Division = ts.Division;
                ctx.Maps.TeamSeasonIdMap[ts.Id] = existing.Id;
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
                ctx.RemoteDb.TeamSeasons.Add(newTs);
                await ctx.RemoteDb.SaveChangesAsync(ct);
                ctx.Maps.TeamSeasonIdMap[ts.Id] = newTs.Id;
            }
        }

        await ctx.RemoteDb.SaveChangesAsync(ct);
        return localTeamSeasons.Count;
    }

    public static async Task<int> PushPlayersAsync(PushExecutionContext ctx, CancellationToken ct)
        => await PushBatchedAsync(
            ctx,
            PushStage.Players,
            () => ctx.LocalDb.Players.AsNoTracking().OrderBy(p => p.Id),
            async (player, ct2) =>
            {
                int? remoteTeamId = player.TeamId.HasValue && ctx.Maps.TeamIdMap.TryGetValue(player.TeamId.Value, out var tid)
                    ? tid
                    : null;

                Player? existing = null;
                if (!string.IsNullOrEmpty(player.EspnId))
                    existing = await ctx.RemoteDb.Players.FirstOrDefaultAsync(p => p.EspnId == player.EspnId, ct2);
                existing ??= await ctx.RemoteDb.Players
                    .FirstOrDefaultAsync(p => p.Name == player.Name && p.TeamId == remoteTeamId, ct2);

                if (existing != null)
                {
                    existing.Position = player.Position;
                    existing.JerseyNumber = player.JerseyNumber;
                    existing.Height = player.Height;
                    existing.Weight = player.Weight;
                    existing.College = player.College;
                    existing.EspnId = player.EspnId;
                    existing.TeamId = remoteTeamId;
                    ctx.Maps.PlayerIdMap[player.Id] = existing.Id;
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
                        EspnId = player.EspnId,
                    };
                    ctx.RemoteDb.Players.Add(newPlayer);
                    await ctx.RemoteDb.SaveChangesAsync(ct2);
                    ctx.Maps.PlayerIdMap[player.Id] = newPlayer.Id;
                }
            },
            "players",
            ct);

    public static async Task<int> PushVenuesAsync(PushExecutionContext ctx, CancellationToken ct)
    {
        var localVenues = await ctx.LocalDb.Venues.AsNoTracking().ToListAsync(ct);
        if (localVenues.Count == 0)
            return 0;

        ctx.Display.PrintInfo($"Pushing {localVenues.Count} venues...");
        foreach (var venue in localVenues)
        {
            var existing = await ctx.RemoteDb.Venues.FirstOrDefaultAsync(v => v.EspnId == venue.EspnId, ct);
            if (existing != null)
            {
                existing.Name = venue.Name;
                existing.City = venue.City;
                existing.State = venue.State;
                existing.Country = venue.Country;
                existing.IsGrass = venue.IsGrass;
                existing.IsIndoor = venue.IsIndoor;
                ctx.Maps.VenueIdMap[venue.Id] = existing.Id;
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
                    IsIndoor = venue.IsIndoor,
                };
                ctx.RemoteDb.Venues.Add(newVenue);
                await ctx.RemoteDb.SaveChangesAsync(ct);
                ctx.Maps.VenueIdMap[venue.Id] = newVenue.Id;
            }
        }

        await ctx.RemoteDb.SaveChangesAsync(ct);
        return localVenues.Count;
    }

    public static async Task<int> PushGamesAsync(PushExecutionContext ctx, CancellationToken ct)
        => await PushBatchedAsync(
            ctx,
            PushStage.Games,
            () => ctx.LocalDb.Games.AsNoTracking().OrderBy(g => g.Id),
            async (game, ct2) =>
            {
                if (!ctx.Maps.TeamSeasonIdMap.TryGetValue(game.HomeTeamSeasonId, out var remoteHomeId)
                    || !ctx.Maps.TeamSeasonIdMap.TryGetValue(game.AwayTeamSeasonId, out var remoteAwayId))
                {
                    ctx.Errors.Add($"Game {game.Season} week {game.Week}: team season ID mapping missing");
                    return;
                }

                var gameDate = PushTime.ToUtc(game.GameDate);
                int? remoteVenueId = game.VenueId.HasValue && ctx.Maps.VenueIdMap.TryGetValue(game.VenueId.Value, out var vid)
                    ? vid
                    : null;

                var existing = await ctx.RemoteDb.Games.FirstOrDefaultAsync(g =>
                    g.Season == game.Season
                    && g.SeasonType == game.SeasonType
                    && g.Week == game.Week
                    && g.HomeTeamSeasonId == remoteHomeId
                    && g.AwayTeamSeasonId == remoteAwayId, ct2);

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
                    ctx.Maps.GameIdMap[game.Id] = existing.Id;
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
                        BroadcastNetworks = game.BroadcastNetworks,
                    };
                    ctx.RemoteDb.Games.Add(newGame);
                    await ctx.RemoteDb.SaveChangesAsync(ct2);
                    ctx.Maps.GameIdMap[game.Id] = newGame.Id;
                }
            },
            "games",
            ct);

    public static async Task<int> PushPlayerGameStatsAsync(PushExecutionContext ctx, CancellationToken ct)
        => await PushBatchedAsync(
            ctx,
            PushStage.PlayerGameStats,
            () => ctx.LocalDb.PlayerGameStats.AsNoTracking().OrderBy(s => s.Id),
            async (stat, ct2) =>
            {
                if (!ctx.Maps.PlayerIdMap.TryGetValue(stat.PlayerId, out var remotePlayerId)
                    || !ctx.Maps.GameIdMap.TryGetValue(stat.GameId, out var remoteGameId))
                {
                    ctx.Errors.Add($"Stat record: player/game ID mapping missing (P:{stat.PlayerId} G:{stat.GameId})");
                    return;
                }

                var existing = await ctx.RemoteDb.PlayerGameStats
                    .FirstOrDefaultAsync(s => s.PlayerId == remotePlayerId && s.GameId == remoteGameId, ct2);

                if (existing != null)
                    DatabasePushCopiers.CopyAllPlayerStats(stat, existing);
                else
                {
                    var newStat = new PlayerGameStats { PlayerId = remotePlayerId, GameId = remoteGameId };
                    DatabasePushCopiers.CopyAllPlayerStats(stat, newStat);
                    ctx.RemoteDb.PlayerGameStats.Add(newStat);
                }
            },
            "player stats",
            ct);

    public static async Task<int> PushTeamGameStatsAsync(PushExecutionContext ctx, CancellationToken ct)
        => await PushBatchedAsync(
            ctx,
            PushStage.TeamGameStats,
            () => ctx.LocalDb.TeamGameStats.AsNoTracking().OrderBy(s => s.Id),
            async (tgs, ct2) =>
            {
                if (!ctx.Maps.GameIdMap.TryGetValue(tgs.GameId, out var remoteGameId)
                    || !ctx.Maps.TeamSeasonIdMap.TryGetValue(tgs.TeamSeasonId, out var remoteTeamSeasonId))
                {
                    ctx.Errors.Add($"TeamGameStats: game/team season ID mapping missing (G:{tgs.GameId} TS:{tgs.TeamSeasonId})");
                    return;
                }

                var existing = await ctx.RemoteDb.TeamGameStats
                    .FirstOrDefaultAsync(t => t.GameId == remoteGameId && t.TeamSeasonId == remoteTeamSeasonId, ct2);

                if (existing != null)
                    DatabasePushCopiers.CopyAllTeamGameStats(tgs, existing);
                else
                {
                    var newTgs = new TeamGameStats { GameId = remoteGameId, TeamSeasonId = remoteTeamSeasonId };
                    DatabasePushCopiers.CopyAllTeamGameStats(tgs, newTgs);
                    ctx.RemoteDb.TeamGameStats.Add(newTgs);
                }
            },
            "team game stats",
            ct);

    public static async Task<int> PushInjuriesAsync(PushExecutionContext ctx, CancellationToken ct)
        => await PushBatchedAsync(
            ctx,
            PushStage.Injuries,
            () => ctx.LocalDb.Injuries.AsNoTracking().OrderBy(i => i.Id),
            async (injury, ct2) =>
            {
                if (!ctx.Maps.GameIdMap.TryGetValue(injury.GameId, out var remoteGameId))
                {
                    ctx.Errors.Add($"Injury: game ID mapping missing (G:{injury.GameId})");
                    return;
                }

                int? remotePlayerId = injury.PlayerId.HasValue && ctx.Maps.PlayerIdMap.TryGetValue(injury.PlayerId.Value, out var pid)
                    ? pid
                    : null;

                var existing = await ctx.RemoteDb.Injuries
                    .FirstOrDefaultAsync(i => i.GameId == remoteGameId && i.EspnAthleteId == injury.EspnAthleteId, ct2);

                if (existing != null)
                {
                    existing.PlayerId = remotePlayerId;
                    existing.PlayerName = injury.PlayerName;
                    existing.Status = injury.Status;
                    existing.InjuryType = injury.InjuryType;
                    existing.BodyLocation = injury.BodyLocation;
                    existing.Side = injury.Side;
                    existing.Detail = injury.Detail;
                    existing.ReturnDate = PushTime.ToUtcOrNull(injury.ReturnDate);
                    existing.ReportDate = PushTime.ToUtc(injury.ReportDate);
                }
                else
                {
                    ctx.RemoteDb.Injuries.Add(new Injury
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
                        ReturnDate = PushTime.ToUtcOrNull(injury.ReturnDate),
                        ReportDate = PushTime.ToUtc(injury.ReportDate),
                    });
                }
            },
            "injuries",
            ct);

    public static async Task<int> PushApiLinksAsync(PushExecutionContext ctx, CancellationToken ct)
        => await PushBatchedAsync(
            ctx,
            PushStage.ApiLinks,
            () => ctx.LocalDb.ApiLinks.AsNoTracking().OrderBy(l => l.Id),
            async (link, ct2) =>
            {
                int? remoteGameId = link.GameId.HasValue && ctx.Maps.GameIdMap.TryGetValue(link.GameId.Value, out var gid)
                    ? gid
                    : null;
                int? remoteLinkTeamId = link.TeamId.HasValue && ctx.Maps.TeamIdMap.TryGetValue(link.TeamId.Value, out var tid)
                    ? tid
                    : null;

                var existing = await ctx.RemoteDb.ApiLinks.FirstOrDefaultAsync(a => a.Url == link.Url, ct2);
                if (existing != null)
                {
                    existing.EndpointType = link.EndpointType;
                    existing.RelationType = link.RelationType;
                    existing.GameId = remoteGameId;
                    existing.TeamId = remoteLinkTeamId;
                    existing.Season = link.Season;
                    existing.Week = link.Week;
                    existing.EspnEventId = link.EspnEventId;
                    existing.DiscoveredAt = PushTime.ToUtc(link.DiscoveredAt);
                    existing.LastAccessedAt = PushTime.ToUtcOrNull(link.LastAccessedAt);
                }
                else
                {
                    ctx.RemoteDb.ApiLinks.Add(new ApiLink
                    {
                        Url = link.Url,
                        EndpointType = link.EndpointType,
                        RelationType = link.RelationType,
                        GameId = remoteGameId,
                        TeamId = remoteLinkTeamId,
                        Season = link.Season,
                        Week = link.Week,
                        EspnEventId = link.EspnEventId,
                        DiscoveredAt = PushTime.ToUtc(link.DiscoveredAt),
                        LastAccessedAt = PushTime.ToUtcOrNull(link.LastAccessedAt),
                    });
                }
            },
            "API links",
            ct);

    public static async Task<int> PushGameDrivesAsync(PushExecutionContext ctx, CancellationToken ct)
        => await PushTier1BatchedAsync(ctx, PushStage.GameDrives,
            () => ctx.LocalDb.GameDrives.AsNoTracking().OrderBy(d => d.Id),
            async (drive, ct2) =>
            {
                if (!ctx.Maps.GameIdMap.TryGetValue(drive.GameId, out var remoteGameId))
                    return;

                int? remoteTeamSeasonId = drive.TeamSeasonId.HasValue && ctx.Maps.TeamSeasonIdMap.TryGetValue(drive.TeamSeasonId.Value, out var tsid)
                    ? tsid
                    : null;

                var existing = await ctx.RemoteDb.GameDrives
                    .FirstOrDefaultAsync(d => d.GameId == remoteGameId && d.EspnDriveId == drive.EspnDriveId, ct2);

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
                    ctx.RemoteDb.GameDrives.Add(new GameDrive
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
                        DataSourceFetchedAt = PushTime.ToUtcOrNull(drive.DataSourceFetchedAt),
                        DataSourceRecordId = drive.DataSourceRecordId,
                        CreatedAt = PushTime.ToUtc(drive.CreatedAt),
                        UpdatedAt = PushTime.ToUtc(drive.UpdatedAt),
                    });
                }
            },
            "game drives",
            ct);

    public static async Task<int> PushScoringPlaysAsync(PushExecutionContext ctx, CancellationToken ct)
        => await PushTier1BatchedAsync(ctx, PushStage.ScoringPlays,
            () => ctx.LocalDb.ScoringPlays.AsNoTracking().OrderBy(p => p.Id),
            async (play, ct2) =>
            {
                if (!ctx.Maps.GameIdMap.TryGetValue(play.GameId, out var remoteGameId))
                    return;

                int? remoteTeamSeasonId = play.TeamSeasonId.HasValue && ctx.Maps.TeamSeasonIdMap.TryGetValue(play.TeamSeasonId.Value, out var tsid)
                    ? tsid
                    : null;

                var existing = await ctx.RemoteDb.ScoringPlays
                    .FirstOrDefaultAsync(p => p.GameId == remoteGameId && p.EspnPlayId == play.EspnPlayId, ct2);

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
                    ctx.RemoteDb.ScoringPlays.Add(new ScoringPlay
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
                        DataSourceFetchedAt = PushTime.ToUtcOrNull(play.DataSourceFetchedAt),
                        DataSourceRecordId = play.DataSourceRecordId,
                        CreatedAt = PushTime.ToUtc(play.CreatedAt),
                        UpdatedAt = PushTime.ToUtc(play.UpdatedAt),
                    });
                }
            },
            "scoring plays",
            ct);

    public static async Task<int> PushGameWeatherAsync(PushExecutionContext ctx, CancellationToken ct)
        => await PushTier1BatchedAsync(ctx, PushStage.GameWeather,
            () => ctx.LocalDb.GameWeathers.AsNoTracking().OrderBy(w => w.Id),
            async (weather, ct2) =>
            {
                if (!ctx.Maps.GameIdMap.TryGetValue(weather.GameId, out var remoteGameId))
                    return;

                var existing = await ctx.RemoteDb.GameWeathers.FirstOrDefaultAsync(w => w.GameId == remoteGameId, ct2);
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
                    ctx.RemoteDb.GameWeathers.Add(new GameWeather
                    {
                        GameId = remoteGameId,
                        TemperatureF = weather.TemperatureF,
                        HighTemperatureF = weather.HighTemperatureF,
                        Condition = weather.Condition,
                        WindSpeedMph = weather.WindSpeedMph,
                        WindDirection = weather.WindDirection,
                        HumidityPercent = weather.HumidityPercent,
                        DataSource = weather.DataSource,
                        DataSourceFetchedAt = PushTime.ToUtcOrNull(weather.DataSourceFetchedAt),
                        CreatedAt = PushTime.ToUtc(weather.CreatedAt),
                        UpdatedAt = PushTime.ToUtc(weather.UpdatedAt),
                    });
                }
            },
            "game weather",
            ct);

    public static async Task<int> PushGameOfficialsAsync(PushExecutionContext ctx, CancellationToken ct)
        => await PushTier1BatchedAsync(ctx, PushStage.GameOfficials,
            () => ctx.LocalDb.GameOfficials.AsNoTracking().OrderBy(o => o.Id),
            async (official, ct2) =>
            {
                if (!ctx.Maps.GameIdMap.TryGetValue(official.GameId, out var remoteGameId))
                    return;

                var existing = await ctx.RemoteDb.GameOfficials.FirstOrDefaultAsync(o =>
                    o.GameId == remoteGameId && o.Name == official.Name && o.Position == official.Position, ct2);

                if (existing != null)
                    existing.SortOrder = official.SortOrder;
                else
                {
                    ctx.RemoteDb.GameOfficials.Add(new GameOfficial
                    {
                        GameId = remoteGameId,
                        Name = official.Name,
                        Position = official.Position,
                        SortOrder = official.SortOrder,
                        DataSource = official.DataSource,
                        DataSourceFetchedAt = PushTime.ToUtcOrNull(official.DataSourceFetchedAt),
                        CreatedAt = PushTime.ToUtc(official.CreatedAt),
                        UpdatedAt = PushTime.ToUtc(official.UpdatedAt),
                    });
                }
            },
            "game officials",
            ct);

    public static async Task<int> PushGameOddsAsync(PushExecutionContext ctx, CancellationToken ct)
        => await PushTier1BatchedAsync(ctx, PushStage.GameOdds,
            () => ctx.LocalDb.GameOdds.AsNoTracking().OrderBy(o => o.Id),
            async (odds, ct2) =>
            {
                if (!ctx.Maps.GameIdMap.TryGetValue(odds.GameId, out var remoteGameId))
                    return;

                var capturedAt = PushTime.ToUtc(odds.CapturedAt);
                var exists = await ctx.RemoteDb.GameOdds.AnyAsync(o =>
                    o.GameId == remoteGameId
                    && o.Sportsbook == odds.Sportsbook
                    && o.SnapshotType == odds.SnapshotType
                    && o.CapturedAt == capturedAt, ct2);

                if (!exists)
                {
                    ctx.RemoteDb.GameOdds.Add(new GameOdds
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
                        DataSourceFetchedAt = PushTime.ToUtcOrNull(odds.DataSourceFetchedAt),
                        CreatedAt = PushTime.ToUtc(odds.CreatedAt),
                        UpdatedAt = PushTime.ToUtc(odds.UpdatedAt),
                    });
                }
            },
            "game odds",
            ct);

    private static async Task<int> PushBatchedAsync<T>(
        PushExecutionContext ctx,
        PushStage stage,
        Func<IQueryable<T>> queryFactory,
        Func<T, CancellationToken, Task> processOne,
        string label,
        CancellationToken ct) where T : class
    {
        var total = await queryFactory().CountAsync(ct);
        if (total == 0)
            return 0;

        var offset = ctx.Session.CurrentStage == stage ? ctx.Session.StageOffset : 0;
        var processed = 0;

        if (offset == 0)
            ctx.Display.PrintInfo($"Pushing {total} {label}...");

        while (offset < total)
        {
            var batch = await queryFactory().Skip(offset).Take(ctx.BatchSize).ToListAsync(ct);
            if (batch.Count == 0)
                break;

            foreach (var item in batch)
                await processOne(item, ct);

            await ctx.RemoteDb.SaveChangesAsync(ct);
            offset += batch.Count;
            processed += batch.Count;
            ctx.Session.StageOffset = offset;
            await DatabasePushSessionStore.SaveAsync(ctx.LocalDb, ctx.Session, ct);
        }

        ctx.Display.PrintSuccess($"{char.ToUpper(label[0])}{label[1..]}: {total} pushed");
        return total;
    }

    private static Task<int> PushTier1BatchedAsync<T>(
        PushExecutionContext ctx,
        PushStage stage,
        Func<IQueryable<T>> queryFactory,
        Func<T, CancellationToken, Task> processOne,
        string label,
        CancellationToken ct) where T : class
        => PushBatchedAsync(ctx, stage, queryFactory, processOne, label, ct);
}
