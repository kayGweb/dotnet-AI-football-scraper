using System.Text.Json.Serialization;

namespace WebScraper.Services.Scrapers.NflCom;

// --- Teams ---
// Endpoint: /teams
// Response: { "teams": [ { ... } ] }

public class NflComTeamsResponse
{
    [JsonPropertyName("teams")]
    public List<NflComTeam> Teams { get; set; } = new();
}

public class NflComTeam
{
    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("nickName")]
    public string NickName { get; set; } = string.Empty;

    [JsonPropertyName("cityStateRegion")]
    public string CityStateRegion { get; set; } = string.Empty;

    [JsonPropertyName("conference")]
    public string Conference { get; set; } = string.Empty;

    [JsonPropertyName("division")]
    public string Division { get; set; } = string.Empty;
}

// --- Roster ---
// Endpoint: /teams/{abbr}/roster
// Response: { "roster": [ { ... } ] }

public class NflComRosterResponse
{
    [JsonPropertyName("roster")]
    public List<NflComPlayer> Roster { get; set; } = new();
}

public class NflComPlayer
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("jerseyNumber")]
    public string? JerseyNumber { get; set; }

    [JsonPropertyName("position")]
    public string Position { get; set; } = string.Empty;

    [JsonPropertyName("height")]
    public string? Height { get; set; }

    [JsonPropertyName("weight")]
    public string? Weight { get; set; }

    [JsonPropertyName("college")]
    public string? College { get; set; }
}

// --- Games ---
// Endpoint: /games?season={year}&seasonType=REG&week={n}
// Response: { "games": [ { ... } ] }

public class NflComGamesResponse
{
    [JsonPropertyName("games")]
    public List<NflComGame> Games { get; set; } = new();
}

public class NflComGame
{
    [JsonPropertyName("gameDetailId")]
    public string GameDetailId { get; set; } = string.Empty;

    [JsonPropertyName("week")]
    public int Week { get; set; }

    [JsonPropertyName("gameDate")]
    public string? GameDate { get; set; }

    [JsonPropertyName("homeTeam")]
    public NflComGameTeam HomeTeam { get; set; } = new();

    [JsonPropertyName("awayTeam")]
    public NflComGameTeam AwayTeam { get; set; } = new();

    [JsonPropertyName("homeTeamScore")]
    public NflComScore? HomeTeamScore { get; set; }

    [JsonPropertyName("awayTeamScore")]
    public NflComScore? AwayTeamScore { get; set; }
}

public class NflComGameTeam
{
    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;
}

public class NflComScore
{
    [JsonPropertyName("pointTotal")]
    public int? PointTotal { get; set; }
}

// --- Game Stats ---
// Endpoint: /games/{gameDetailId}/stats
// Response: { "homeTeamStats": { "playerStats": [ ... ] }, "awayTeamStats": { "playerStats": [ ... ] } }

public class NflComGameStatsResponse
{
    [JsonPropertyName("homeTeamStats")]
    public NflComTeamStats? HomeTeamStats { get; set; }

    [JsonPropertyName("awayTeamStats")]
    public NflComTeamStats? AwayTeamStats { get; set; }
}

public class NflComTeamStats
{
    [JsonPropertyName("playerStats")]
    public List<NflComPlayerStats> PlayerStats { get; set; } = new();
}

public class NflComPlayerStats
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("passing")]
    public NflComPassingStats? Passing { get; set; }

    [JsonPropertyName("rushing")]
    public NflComRushingStats? Rushing { get; set; }

    [JsonPropertyName("receiving")]
    public NflComReceivingStats? Receiving { get; set; }
}

public class NflComPassingStats
{
    [JsonPropertyName("completions")]
    public int Completions { get; set; }

    [JsonPropertyName("attempts")]
    public int Attempts { get; set; }

    [JsonPropertyName("yards")]
    public int Yards { get; set; }

    [JsonPropertyName("touchdowns")]
    public int Touchdowns { get; set; }

    [JsonPropertyName("interceptions")]
    public int Interceptions { get; set; }
}

public class NflComRushingStats
{
    [JsonPropertyName("attempts")]
    public int Attempts { get; set; }

    [JsonPropertyName("yards")]
    public int Yards { get; set; }

    [JsonPropertyName("touchdowns")]
    public int Touchdowns { get; set; }
}

public class NflComReceivingStats
{
    [JsonPropertyName("receptions")]
    public int Receptions { get; set; }

    [JsonPropertyName("yards")]
    public int Yards { get; set; }

    [JsonPropertyName("touchdowns")]
    public int Touchdowns { get; set; }
}
