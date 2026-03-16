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

    [JsonPropertyName("venue")]
    public EspnVenue? Venue { get; set; }

    [JsonPropertyName("attendance")]
    public int? Attendance { get; set; }

    [JsonPropertyName("neutralSite")]
    public bool NeutralSite { get; set; }

    [JsonPropertyName("status")]
    public EspnStatus? Status { get; set; }
}

public class EspnCompetitor
{
    [JsonPropertyName("homeAway")]
    public string HomeAway { get; set; } = string.Empty;

    [JsonPropertyName("team")]
    public EspnCompetitorTeam Team { get; set; } = new();

    [JsonPropertyName("score")]
    public string? Score { get; set; }

    [JsonPropertyName("winner")]
    public bool? Winner { get; set; }

    [JsonPropertyName("linescores")]
    public List<EspnLinescore>? Linescores { get; set; }

    [JsonPropertyName("records")]
    public List<EspnRecord>? Records { get; set; }
}

public class EspnCompetitorTeam
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;
}

public class EspnLinescore
{
    [JsonPropertyName("value")]
    public double Value { get; set; }
}

public class EspnRecord
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}

public class EspnStatus
{
    [JsonPropertyName("type")]
    public EspnStatusType? Type { get; set; }
}

public class EspnStatusType
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("completed")]
    public bool Completed { get; set; }
}

// --- Venue ---

public class EspnVenue
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public EspnAddress? Address { get; set; }

    [JsonPropertyName("grass")]
    public bool Grass { get; set; }

    [JsonPropertyName("indoor")]
    public bool Indoor { get; set; }
}

public class EspnAddress
{
    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;
}

// --- Game Summary (Stats) ---

public class EspnSummaryResponse
{
    [JsonPropertyName("boxscore")]
    public EspnBoxscore? Boxscore { get; set; }

    [JsonPropertyName("gameInfo")]
    public EspnGameInfo? GameInfo { get; set; }

    [JsonPropertyName("injuries")]
    public List<EspnInjuryTeam>? Injuries { get; set; }

    [JsonPropertyName("header")]
    public EspnHeader? Header { get; set; }
}

public class EspnBoxscore
{
    [JsonPropertyName("players")]
    public List<EspnBoxscoreTeam> Players { get; set; } = new();

    [JsonPropertyName("teams")]
    public List<EspnBoxscoreTeamStats>? Teams { get; set; }
}

public class EspnBoxscoreTeamStats
{
    [JsonPropertyName("team")]
    public EspnCompetitorTeam Team { get; set; } = new();

    [JsonPropertyName("statistics")]
    public List<EspnTeamStatistic> Statistics { get; set; } = new();
}

public class EspnTeamStatistic
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayValue")]
    public string DisplayValue { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
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

// --- GameInfo (venue, attendance, officials) ---

public class EspnGameInfo
{
    [JsonPropertyName("venue")]
    public EspnVenue? Venue { get; set; }

    [JsonPropertyName("attendance")]
    public int? Attendance { get; set; }

    [JsonPropertyName("officials")]
    public List<EspnOfficial>? Officials { get; set; }
}

public class EspnOfficial
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("position")]
    public EspnOfficialPosition? Position { get; set; }
}

public class EspnOfficialPosition
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

// --- Injuries ---

public class EspnInjuryTeam
{
    [JsonPropertyName("team")]
    public EspnCompetitorTeam Team { get; set; } = new();

    [JsonPropertyName("injuries")]
    public List<EspnInjuryEntry> Injuries { get; set; } = new();
}

public class EspnInjuryEntry
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("athlete")]
    public EspnInjuryAthlete Athlete { get; set; } = new();

    [JsonPropertyName("type")]
    public EspnInjuryType? Type { get; set; }

    [JsonPropertyName("details")]
    public EspnInjuryDetails? Details { get; set; }
}

public class EspnInjuryAthlete
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public class EspnInjuryType
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;
}

public class EspnInjuryDetails
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("returnDate")]
    public string? ReturnDate { get; set; }
}

// --- Header (links) ---

public class EspnHeader
{
    [JsonPropertyName("links")]
    public List<EspnHeaderLink>? Links { get; set; }

    [JsonPropertyName("competitions")]
    public List<EspnHeaderCompetition>? Competitions { get; set; }
}

public class EspnHeaderLink
{
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("rel")]
    public List<string>? Rel { get; set; }
}

public class EspnHeaderCompetition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("competitors")]
    public List<EspnCompetitor> Competitors { get; set; } = new();

    [JsonPropertyName("status")]
    public EspnStatus? Status { get; set; }
}
