using System.Text.Json.Serialization;

namespace WebScraper.Services.Scrapers.Espn;

// --- Teams ---

public class EspnTeamsResponse
{
    [JsonPropertyName("sports")]
    public List<EspnSport> Sports { get; set; } = new();
}

public class EspnSport
{
    [JsonPropertyName("leagues")]
    public List<EspnLeague> Leagues { get; set; } = new();
}

public class EspnLeague
{
    [JsonPropertyName("teams")]
    public List<EspnTeamWrapper> Teams { get; set; } = new();
}

public class EspnTeamWrapper
{
    [JsonPropertyName("team")]
    public EspnTeam Team { get; set; } = new();
}

public class EspnTeam
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("shortDisplayName")]
    public string ShortDisplayName { get; set; } = string.Empty;
}

// --- Roster ---

public class EspnRosterResponse
{
    [JsonPropertyName("athletes")]
    public List<EspnRosterCategory> Athletes { get; set; } = new();
}

public class EspnRosterCategory
{
    [JsonPropertyName("position")]
    public string Position { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<EspnAthlete> Items { get; set; } = new();
}

public class EspnAthlete
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("jersey")]
    public string? Jersey { get; set; }

    [JsonPropertyName("position")]
    public EspnPosition? Position { get; set; }

    [JsonPropertyName("height")]
    public double? Height { get; set; }

    [JsonPropertyName("weight")]
    public double? Weight { get; set; }

    [JsonPropertyName("college")]
    public EspnCollege? College { get; set; }
}

public class EspnPosition
{
    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;
}

public class EspnCollege
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

// --- Scoreboard ---

public class EspnScoreboardResponse
{
    [JsonPropertyName("events")]
    public List<EspnEvent> Events { get; set; } = new();
}

public class EspnEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("season")]
    public EspnSeason? Season { get; set; }

    [JsonPropertyName("week")]
    public EspnWeek? Week { get; set; }

    [JsonPropertyName("competitions")]
    public List<EspnCompetition> Competitions { get; set; } = new();
}

public class EspnSeason
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }
}

public class EspnWeek
{
    [JsonPropertyName("number")]
    public int Number { get; set; }
}

public class EspnCompetition
{
    [JsonPropertyName("competitors")]
    public List<EspnCompetitor> Competitors { get; set; } = new();
}

public class EspnCompetitor
{
    [JsonPropertyName("homeAway")]
    public string HomeAway { get; set; } = string.Empty;

    [JsonPropertyName("team")]
    public EspnCompetitorTeam Team { get; set; } = new();

    [JsonPropertyName("score")]
    public string? Score { get; set; }
}

public class EspnCompetitorTeam
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;
}

// --- Game Summary (Stats) ---

public class EspnSummaryResponse
{
    [JsonPropertyName("boxscore")]
    public EspnBoxscore? Boxscore { get; set; }
}

public class EspnBoxscore
{
    [JsonPropertyName("players")]
    public List<EspnBoxscoreTeam> Players { get; set; } = new();
}

public class EspnBoxscoreTeam
{
    [JsonPropertyName("team")]
    public EspnCompetitorTeam Team { get; set; } = new();

    [JsonPropertyName("statistics")]
    public List<EspnStatCategory> Statistics { get; set; } = new();
}

public class EspnStatCategory
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("keys")]
    public List<string> Keys { get; set; } = new();

    [JsonPropertyName("athletes")]
    public List<EspnStatAthlete> Athletes { get; set; } = new();
}

public class EspnStatAthlete
{
    [JsonPropertyName("athlete")]
    public EspnStatAthleteInfo Athlete { get; set; } = new();

    [JsonPropertyName("stats")]
    public List<string> Stats { get; set; } = new();
}

public class EspnStatAthleteInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}
