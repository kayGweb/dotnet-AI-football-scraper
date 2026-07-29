namespace WebScraper.Services.Agent;

/// <summary>
/// Static schema metadata for agent introspection (no raw SQL).
/// </summary>
public static class SchemaDescriptionService
{
    private static readonly IReadOnlyDictionary<string, EntitySchema> Entities = new Dictionary<string, EntitySchema>(StringComparer.OrdinalIgnoreCase)
    {
        ["Team"] = new("Team", "Current NFL team row (mutable convenience; historical identity is TeamSeason/Franchise).",
            ["Id:int:PK", "Name:string", "Abbreviation:string:UK", "City:string", "Conference:string", "Division:string"],
            []),
        ["Franchise"] = new("Franchise", "Stable franchise identity across relocations/rebrands.",
            ["Id:int:PK", "CanonicalAbbreviation:string:UK", "DisplayName:string"],
            []),
        ["TeamSeason"] = new("TeamSeason", "Team identity as of a specific season (name, city, abbr).",
            ["Id:int:PK", "FranchiseId:int:FK→Franchise", "Season:int", "Name:string", "Abbreviation:string", "Conference:string", "Division:string"],
            ["Game.HomeTeamSeasonId", "Game.AwayTeamSeasonId"]),
        ["Player"] = new("Player", "Player identity keyed on EspnId when available.",
            ["Id:int:PK", "EspnId:string:UK?", "Name:string", "Position:string", "TeamId:int?:FK→Team"],
            ["PlayerGameStats.PlayerId", "PlayerTeamSeason.PlayerId"]),
        ["Game"] = new("Game", "One NFL game; FKs point at TeamSeason rows.",
            ["Id:int:PK", "Season:int", "SeasonType:enum(Preseason=1,Regular=2,Postseason=3)", "Week:int",
             "HomeTeamSeasonId:int:FK", "AwayTeamSeasonId:int:FK", "HomeScore:int?", "AwayScore:int?", "VenueId:int?:FK",
             "BroadcastNetworks:string?"],
            ["PlayerGameStats.GameId", "TeamGameStats.GameId", "Injury.GameId", "GameDrive.GameId", "ScoringPlay.GameId", "GameWeather.GameId", "GameOfficial.GameId", "GameOdds.GameId"]),
        ["PlayerGameStats"] = new("PlayerGameStats", "Per-game player stat line (~40 columns across 10 categories).",
            ["Id:int:PK", "PlayerId:int:FK", "GameId:int:FK", "PassYards:int", "RushYards:int", "RecYards:int", "..."],
            []),
        ["TeamGameStats"] = new("TeamGameStats", "Team-level per-game aggregates.",
            ["Id:int:PK", "GameId:int:FK", "TeamSeasonId:int:FK", "TotalYards:int", "Turnovers:int", "..."],
            []),
        ["Venue"] = new("Venue", "Stadium keyed on EspnId.",
            ["Id:int:PK", "EspnId:string:UK", "Name:string", "City:string", "State:string", "IsIndoor:bool"],
            ["Game.VenueId"]),
        ["GameDrive"] = new("GameDrive", "Per-game drive chart from ESPN summary.",
            ["Id:int:PK", "GameId:int:FK", "EspnDriveId:string", "Sequence:int", "TeamSeasonId:int?:FK", "Yards:int", "Result:string?"],
            []),
        ["ScoringPlay"] = new("ScoringPlay", "Scoring play with running score.",
            ["Id:int:PK", "GameId:int:FK", "EspnPlayId:string", "Period:int", "Clock:string?", "Description:string", "HomeScore:int", "AwayScore:int"],
            []),
        ["GameWeather"] = new("GameWeather", "Game-day weather (1:1 with Game).",
            ["Id:int:PK", "GameId:int:FK:UK", "TemperatureF:int?", "Condition:string?", "WindSpeedMph:int?"],
            []),
        ["GameOfficial"] = new("GameOfficial", "Referee crew member.",
            ["Id:int:PK", "GameId:int:FK", "Name:string", "Position:string", "SortOrder:int"],
            []),
        ["GameOdds"] = new("GameOdds", "Betting odds snapshot per sportsbook.",
            ["Id:int:PK", "GameId:int:FK", "Sportsbook:string", "Spread:double?", "OverUnder:double?", "HomeMoneyline:int?", "AwayMoneyline:int?", "SnapshotType:enum", "CapturedAt:datetime"],
            []),
        ["SeasonCoverage"] = new("SeasonCoverage", "Expected-vs-actual coverage per (season, seasonType, week).",
            ["Season:int", "SeasonType:enum", "Week:int", "ExpectedGames:int?", "ActualGames:int", "GamesWithPlayerStats:int", "GamesWithOdds:int"],
            []),
    };

    public static object Describe(string? entity = null)
    {
        if (string.IsNullOrWhiteSpace(entity))
        {
            return new
            {
                entities = Entities.Values.Select(e => new { e.Name, e.Description, columnCount = e.Columns.Count }),
            };
        }

        if (!Entities.TryGetValue(entity, out var schema))
            return new { error = true, message = $"Unknown entity '{entity}'. Known: {string.Join(", ", Entities.Keys)}" };

        return new
        {
            schema.Name,
            schema.Description,
            columns = schema.Columns,
            referencedBy = schema.ReferencedBy,
        };
    }

    private sealed record EntitySchema(string Name, string Description, IReadOnlyList<string> Columns, IReadOnlyList<string> ReferencedBy);
}
