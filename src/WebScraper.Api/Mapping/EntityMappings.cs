using WebScraper.Api.Dtos;
using WebScraper.Models;

namespace WebScraper.Api.Mapping;

/// <summary>
/// Explicit entity → DTO mapping. Kept as hand-rolled extension methods rather
/// than introducing AutoMapper — the entity graph is small, mappings rarely
/// change, and hand-rolled mappings are easier for Claude Code to modify safely.
/// </summary>
public static class EntityMappings
{
    public static MetaDto ToMeta(this IAuditableEntity entity) => new()
    {
        Source = entity.DataSource,
        FetchedAt = entity.DataSourceFetchedAt,
        SourceRecordId = entity.DataSourceRecordId,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
    };

    public static TeamDto ToDto(this Team team) => new()
    {
        Id = team.Id,
        Name = team.Name,
        Abbreviation = team.Abbreviation,
        City = team.City,
        Conference = team.Conference,
        Division = team.Division,
        Meta = team.ToMeta(),
    };

    public static TeamSummaryDto ToSummary(this Team team) => new()
    {
        Id = team.Id,
        Name = team.Name,
        Abbreviation = team.Abbreviation,
    };

    public static PlayerDto ToDto(this Player player) => new()
    {
        Id = player.Id,
        Name = player.Name,
        TeamId = player.TeamId,
        TeamAbbreviation = player.Team?.Abbreviation,
        Position = player.Position,
        JerseyNumber = player.JerseyNumber,
        Height = player.Height,
        Weight = player.Weight,
        College = player.College,
        EspnId = player.EspnId,
        Meta = player.ToMeta(),
    };

    public static TeamSummaryDto ToSummary(this TeamSeason teamSeason) => new()
    {
        Id = teamSeason.Id,
        Name = teamSeason.Name,
        Abbreviation = teamSeason.Abbreviation,
    };

    public static GameDto ToDto(this Game game) => new()
    {
        Id = game.Id,
        Season = game.Season,
        SeasonType = game.SeasonType.ToString(),
        Week = game.Week,
        GameDate = game.GameDate,
        GameStatus = game.GameStatus,
        EspnEventId = game.EspnEventId,
        HomeTeam = game.HomeTeamSeason?.ToSummary() ?? new TeamSummaryDto { Id = game.HomeTeamSeasonId },
        AwayTeam = game.AwayTeamSeason?.ToSummary() ?? new TeamSummaryDto { Id = game.AwayTeamSeasonId },
        HomeScore = game.HomeScore,
        AwayScore = game.AwayScore,
        HomeWinner = game.HomeWinner,
        Attendance = game.Attendance,
        NeutralSite = game.NeutralSite,
        BroadcastNetworks = game.BroadcastNetworks,
        Venue = game.Venue?.ToSummary(),
        QuarterScores = HasQuarterScores(game) ? new QuarterScoresDto
        {
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
        } : null,
        Meta = game.ToMeta(),
    };

    public static VenueDto ToDto(this Venue venue) => new()
    {
        Id = venue.Id,
        EspnId = venue.EspnId,
        Name = venue.Name,
        City = venue.City,
        State = venue.State,
        Country = venue.Country,
        IsGrass = venue.IsGrass,
        IsIndoor = venue.IsIndoor,
        Meta = venue.ToMeta(),
    };

    public static VenueSummaryDto ToSummary(this Venue venue) => new()
    {
        Id = venue.Id,
        Name = venue.Name,
        City = venue.City,
        State = venue.State,
        IsIndoor = venue.IsIndoor,
    };

    public static PlayerGameStatsDto ToDto(this PlayerGameStats stats) => new()
    {
        Id = stats.Id,
        PlayerId = stats.PlayerId,
        PlayerName = stats.Player?.Name ?? string.Empty,
        GameId = stats.GameId,
        Season = stats.Game?.Season ?? 0,
        Week = stats.Game?.Week ?? 0,
        Passing = new PassingStatsDto
        {
            Attempts = stats.PassAttempts,
            Completions = stats.PassCompletions,
            Yards = stats.PassYards,
            Touchdowns = stats.PassTouchdowns,
            Interceptions = stats.Interceptions,
            QbRating = stats.QBRating,
            AdjQbr = stats.AdjQBR,
            Sacks = stats.Sacks,
            SackYardsLost = stats.SackYardsLost,
        },
        Rushing = new RushingStatsDto
        {
            Attempts = stats.RushAttempts,
            Yards = stats.RushYards,
            Touchdowns = stats.RushTouchdowns,
            Long = stats.LongRushing,
        },
        Receiving = new ReceivingStatsDto
        {
            Receptions = stats.Receptions,
            Yards = stats.ReceivingYards,
            Touchdowns = stats.ReceivingTouchdowns,
            Targets = stats.ReceivingTargets,
            Long = stats.LongReception,
            YardsPerReception = stats.YardsPerReception,
        },
        Fumbles = new FumblesStatsDto
        {
            Fumbles = stats.Fumbles,
            Lost = stats.FumblesLost,
            Recovered = stats.FumblesRecovered,
        },
        Defensive = new DefensiveStatsDto
        {
            TotalTackles = stats.TotalTackles,
            SoloTackles = stats.SoloTackles,
            Sacks = stats.DefensiveSacks,
            TacklesForLoss = stats.TacklesForLoss,
            PassesDefended = stats.PassesDefended,
            QbHits = stats.QBHits,
            Touchdowns = stats.DefensiveTouchdowns,
        },
        Interceptions = new InterceptionStatsDto
        {
            Caught = stats.InterceptionsCaught,
            Yards = stats.InterceptionYards,
            Touchdowns = stats.InterceptionTouchdowns,
        },
        KickReturns = new KickReturnStatsDto
        {
            Returns = stats.KickReturns,
            Yards = stats.KickReturnYards,
            Long = stats.LongKickReturn,
            Touchdowns = stats.KickReturnTouchdowns,
        },
        PuntReturns = new PuntReturnStatsDto
        {
            Returns = stats.PuntReturns,
            Yards = stats.PuntReturnYards,
            Long = stats.LongPuntReturn,
            Touchdowns = stats.PuntReturnTouchdowns,
        },
        Kicking = new KickingStatsDto
        {
            FieldGoalsMade = stats.FieldGoalsMade,
            FieldGoalAttempts = stats.FieldGoalAttempts,
            LongFieldGoal = stats.LongFieldGoal,
            ExtraPointsMade = stats.ExtraPointsMade,
            ExtraPointAttempts = stats.ExtraPointAttempts,
            TotalKickingPoints = stats.TotalKickingPoints,
        },
        Punting = new PuntingStatsDto
        {
            Punts = stats.Punts,
            Yards = stats.PuntYards,
            GrossAverage = stats.GrossAvgPuntYards,
            Touchbacks = stats.PuntTouchbacks,
            Inside20 = stats.PuntsInside20,
            Long = stats.LongPunt,
        },
        Meta = stats.ToMeta(),
    };

    public static TeamGameStatsDto ToDto(this TeamGameStats stats) => new()
    {
        Id = stats.Id,
        GameId = stats.GameId,
        TeamSeasonId = stats.TeamSeasonId,
        TeamAbbreviation = stats.TeamSeason?.Abbreviation ?? string.Empty,
        FirstDowns = stats.FirstDowns,
        FirstDownsPassing = stats.FirstDownsPassing,
        FirstDownsRushing = stats.FirstDownsRushing,
        FirstDownsPenalty = stats.FirstDownsPenalty,
        ThirdDownMade = stats.ThirdDownMade,
        ThirdDownAttempts = stats.ThirdDownAttempts,
        FourthDownMade = stats.FourthDownMade,
        FourthDownAttempts = stats.FourthDownAttempts,
        TotalPlays = stats.TotalPlays,
        TotalYards = stats.TotalYards,
        NetPassingYards = stats.NetPassingYards,
        PassCompletions = stats.PassCompletions,
        PassAttempts = stats.PassAttempts,
        YardsPerPass = stats.YardsPerPass,
        InterceptionsThrown = stats.InterceptionsThrown,
        SacksAgainst = stats.SacksAgainst,
        SackYardsLost = stats.SackYardsLost,
        RushingYards = stats.RushingYards,
        RushingAttempts = stats.RushingAttempts,
        YardsPerRush = stats.YardsPerRush,
        RedZoneMade = stats.RedZoneMade,
        RedZoneAttempts = stats.RedZoneAttempts,
        Turnovers = stats.Turnovers,
        FumblesLost = stats.FumblesLost,
        Penalties = stats.Penalties,
        PenaltyYards = stats.PenaltyYards,
        DefensiveTouchdowns = stats.DefensiveTouchdowns,
        PossessionTime = stats.PossessionTime,
        Meta = stats.ToMeta(),
    };

    public static InjuryDto ToDto(this Injury injury) => new()
    {
        Id = injury.Id,
        GameId = injury.GameId,
        PlayerId = injury.PlayerId,
        EspnAthleteId = injury.EspnAthleteId,
        PlayerName = injury.PlayerName,
        Status = injury.Status,
        InjuryType = injury.InjuryType,
        BodyLocation = injury.BodyLocation,
        Side = injury.Side,
        Detail = injury.Detail,
        ReturnDate = injury.ReturnDate,
        ReportDate = injury.ReportDate,
        Meta = injury.ToMeta(),
    };

    public static GameDriveDto ToDto(this GameDrive drive) => new()
    {
        Id = drive.Id,
        GameId = drive.GameId,
        EspnDriveId = drive.EspnDriveId,
        Sequence = drive.Sequence,
        TeamAbbreviation = drive.TeamSeason?.Abbreviation,
        Description = drive.Description,
        StartPeriod = drive.StartPeriod,
        EndPeriod = drive.EndPeriod,
        TimeElapsed = drive.TimeElapsed,
        Yards = drive.Yards,
        OffensivePlays = drive.OffensivePlays,
        IsScore = drive.IsScore,
        Result = drive.Result,
        DisplayResult = drive.DisplayResult,
        Meta = drive.ToMeta(),
    };

    public static ScoringPlayDto ToDto(this ScoringPlay play) => new()
    {
        Id = play.Id,
        GameId = play.GameId,
        EspnPlayId = play.EspnPlayId,
        Sequence = play.Sequence,
        TeamAbbreviation = play.TeamSeason?.Abbreviation,
        Period = play.Period,
        Clock = play.Clock,
        PlayType = play.PlayType,
        Description = play.Description,
        HomeScore = play.HomeScore,
        AwayScore = play.AwayScore,
        ScoringType = play.ScoringType,
        Meta = play.ToMeta(),
    };

    public static GameWeatherDto ToDto(this GameWeather weather) => new()
    {
        Id = weather.Id,
        GameId = weather.GameId,
        TemperatureF = weather.TemperatureF,
        HighTemperatureF = weather.HighTemperatureF,
        Condition = weather.Condition,
        WindSpeedMph = weather.WindSpeedMph,
        WindDirection = weather.WindDirection,
        HumidityPercent = weather.HumidityPercent,
        Meta = weather.ToMeta(),
    };

    public static GameOfficialDto ToDto(this GameOfficial official) => new()
    {
        Id = official.Id,
        GameId = official.GameId,
        Name = official.Name,
        Position = official.Position,
        SortOrder = official.SortOrder,
        Meta = official.ToMeta(),
    };

    public static GameOddsDto ToDto(this GameOdds odds) => new()
    {
        Id = odds.Id,
        GameId = odds.GameId,
        Sportsbook = odds.Sportsbook,
        Spread = odds.Spread,
        OverUnder = odds.OverUnder,
        HomeMoneyline = odds.HomeMoneyline,
        AwayMoneyline = odds.AwayMoneyline,
        SnapshotType = odds.SnapshotType.ToString(),
        CapturedAt = odds.CapturedAt,
        Details = odds.Details,
        Meta = odds.ToMeta(),
    };

    private static bool HasQuarterScores(Game game) =>
        game.HomeQ1.HasValue || game.HomeQ2.HasValue || game.HomeQ3.HasValue || game.HomeQ4.HasValue ||
        game.AwayQ1.HasValue || game.AwayQ2.HasValue || game.AwayQ3.HasValue || game.AwayQ4.HasValue;
}
