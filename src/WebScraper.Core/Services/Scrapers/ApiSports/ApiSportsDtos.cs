using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebScraper.Services.Scrapers.ApiSports;

public class ApiSportsEnvelope<T>
{
    [JsonPropertyName("get")]
    public string Get { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public JsonElement Parameters { get; set; }

    [JsonPropertyName("errors")]
    public JsonElement Errors { get; set; }

    [JsonPropertyName("results")]
    public int Results { get; set; }

    [JsonPropertyName("response")]
    public List<T> Response { get; set; } = [];

    public bool HasErrors =>
        Errors.ValueKind switch
        {
            JsonValueKind.Array => Errors.GetArrayLength() > 0,
            JsonValueKind.Object => Errors.EnumerateObject().Any(),
            _ => false
        };
}

public class ApiSportsGameItem
{
    [JsonPropertyName("game")]
    public ApiSportsGameInfo Game { get; set; } = new();

    [JsonPropertyName("league")]
    public ApiSportsLeagueInfo League { get; set; } = new();

    [JsonPropertyName("teams")]
    public ApiSportsGameTeams Teams { get; set; } = new();

    [JsonPropertyName("scores")]
    public ApiSportsGameScores Scores { get; set; } = new();
}

public class ApiSportsGameInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("stage")]
    public string? Stage { get; set; }

    [JsonPropertyName("week")]
    public string? Week { get; set; }

    [JsonPropertyName("date")]
    public ApiSportsGameDate Date { get; set; } = new();

    [JsonPropertyName("venue")]
    public ApiSportsVenue? Venue { get; set; }

    [JsonPropertyName("status")]
    public ApiSportsGameStatus Status { get; set; } = new();
}

public class ApiSportsGameDate
{
    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}

public class ApiSportsVenue
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }
}

public class ApiSportsGameStatus
{
    [JsonPropertyName("short")]
    public string? Short { get; set; }

    [JsonPropertyName("long")]
    public string? Long { get; set; }

    [JsonPropertyName("timer")]
    public string? Timer { get; set; }
}

public class ApiSportsLeagueInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("season")]
    public string? Season { get; set; }

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }

    [JsonPropertyName("country")]
    public ApiSportsCountry? Country { get; set; }
}

public class ApiSportsCountry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("flag")]
    public string? Flag { get; set; }
}

public class ApiSportsGameTeams
{
    [JsonPropertyName("home")]
    public ApiSportsTeamRef Home { get; set; } = new();

    [JsonPropertyName("away")]
    public ApiSportsTeamRef Away { get; set; } = new();
}

public class ApiSportsTeamRef
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
}

public class ApiSportsGameScores
{
    [JsonPropertyName("home")]
    public ApiSportsSideScores Home { get; set; } = new();

    [JsonPropertyName("away")]
    public ApiSportsSideScores Away { get; set; } = new();
}

public class ApiSportsSideScores
{
    [JsonPropertyName("quarter_1")]
    public int? Quarter1 { get; set; }

    [JsonPropertyName("quarter_2")]
    public int? Quarter2 { get; set; }

    [JsonPropertyName("quarter_3")]
    public int? Quarter3 { get; set; }

    [JsonPropertyName("quarter_4")]
    public int? Quarter4 { get; set; }

    [JsonPropertyName("overtime")]
    public int? Overtime { get; set; }

    [JsonPropertyName("total")]
    public int? Total { get; set; }
}

/// <summary>
/// One element of the <c>/teams?league=1&amp;season=</c> response. The capture
/// (<c>teams.json</c>) returns flat team objects, not a <c>{ "team": … }</c> wrapper.
/// </summary>
public class ApiSportsTeamProfile
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
}

public class ApiSportsPlayer
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("age")]
    public int? Age { get; set; }

    [JsonPropertyName("height")]
    public string? Height { get; set; }

    [JsonPropertyName("weight")]
    public string? Weight { get; set; }

    [JsonPropertyName("college")]
    public string? College { get; set; }

    [JsonPropertyName("group")]
    public string? Group { get; set; }

    [JsonPropertyName("position")]
    public string? Position { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("salary")]
    public string? Salary { get; set; }

    [JsonPropertyName("experience")]
    public int? Experience { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }
}

// ---------------------------------------------------------------------------
// /games/statistics/players?id=  (one element per team)
// ---------------------------------------------------------------------------

public class ApiSportsPlayerStatsTeamBlock
{
    [JsonPropertyName("team")]
    public ApiSportsTeamRef Team { get; set; } = new();

    [JsonPropertyName("groups")]
    public List<ApiSportsStatGroup> Groups { get; set; } = [];
}

public class ApiSportsStatGroup
{
    /// <summary>Passing, Rushing, Receiving, Fumbles, Defensive, Interceptions, Kick returns, Punt returns, Kicking, Punting.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("players")]
    public List<ApiSportsPlayerStatLine> Players { get; set; } = [];
}

public class ApiSportsPlayerStatLine
{
    [JsonPropertyName("player")]
    public ApiSportsPlayerRef Player { get; set; } = new();

    [JsonPropertyName("statistics")]
    public List<ApiSportsStatValue> Statistics { get; set; } = [];
}

public class ApiSportsPlayerRef
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }
}

public class ApiSportsStatValue
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// api-sports sends stat values as strings ("24/36", "3-19", "91.4"), but a plain number
    /// would also be valid JSON, so the raw element is kept and <see cref="Text"/> normalizes it.
    /// </summary>
    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }

    [JsonIgnore]
    public string? Text => ApiSportsJson.ElementToText(Value);
}

// ---------------------------------------------------------------------------
// /games/statistics/teams?id=  (one element per team)
// ---------------------------------------------------------------------------

public class ApiSportsTeamStatsBlock
{
    [JsonPropertyName("team")]
    public ApiSportsTeamRef Team { get; set; } = new();

    /// <summary>
    /// Sections keyed by name (<c>first_downs</c>, <c>plays</c>, <c>yards</c>, <c>passing</c>,
    /// <c>rushings</c>, <c>red_zone</c>, <c>penalties</c>, <c>turnovers</c>, <c>posession</c> [sic], …).
    /// Each section is an object whose values are ints or strings, so it is kept as a
    /// <see cref="JsonElement"/> and read by <see cref="ApiSportsStatMappings"/>.
    /// </summary>
    [JsonPropertyName("statistics")]
    public Dictionary<string, JsonElement> Statistics { get; set; } = new();
}

public static class ApiSportsJson
{
    public static string? ElementToText(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };
}
