namespace WebScraper.Services.Agent;

/// <summary>
/// Stat column meanings and data lineage for agent introspection.
/// </summary>
public static class DataDictionaryService
{
    public static object GetDictionary() => new
    {
        source = "ESPN /summary box score (primary), PFR HTML (legacy)",
        playerGameStats = new Dictionary<string, string>
        {
            ["PassAttempts"] = "Passing attempts",
            ["PassCompletions"] = "Passing completions",
            ["PassYards"] = "Passing yards",
            ["PassTouchdowns"] = "Passing touchdowns",
            ["InterceptionsThrown"] = "Interceptions thrown",
            ["RushAttempts"] = "Rushing attempts",
            ["RushYards"] = "Rushing yards",
            ["RushTouchdowns"] = "Rushing touchdowns",
            ["Receptions"] = "Receptions",
            ["RecYards"] = "Receiving yards",
            ["RecTouchdowns"] = "Receiving touchdowns",
            ["Tackles"] = "Total tackles (defensive)",
            ["Sacks"] = "Sacks (defensive)",
            ["FieldGoalsMade"] = "Field goals made",
            ["ExtraPointsMade"] = "Extra points made",
        },
        teamGameStats = new Dictionary<string, string>
        {
            ["FirstDowns"] = "Total first downs",
            ["TotalYards"] = "Total offensive yards",
            ["Turnovers"] = "Turnovers committed",
            ["Penalties"] = "Penalty count",
            ["PenaltyYards"] = "Penalty yards",
            ["TimeOfPossession"] = "Possession time (seconds)",
        },
        gameFields = new Dictionary<string, string>
        {
            ["SeasonType"] = "1=Preseason, 2=Regular, 3=Postseason (ESPN seasontype)",
            ["HomeTeamSeasonId"] = "FK to TeamSeason for home team in that season",
            ["AwayTeamSeasonId"] = "FK to TeamSeason for away team in that season",
            ["EspnEventId"] = "ESPN event ID — use for stats scrape via /summary?event=",
        },
        coverageRules = new[]
        {
            "Call nfl_get_coverage before league-wide leaderboards — partial seasons return wrong answers.",
            "Playoffs are seasonType=3; Super Bowl is postseason week 4.",
            "Historical players are discovered from box scores, not roster endpoints.",
        },
    };
}
