using System.Text.Json.Serialization;

namespace WebScraper.Services.Scrapers.SportsDataIo;

// --- Teams ---
// Endpoint: /scores/json/Teams
// Response: flat array of team objects

public class SportsDataTeamDto
{
    [JsonPropertyName("TeamID")]
    public int TeamId { get; set; }

    [JsonPropertyName("Key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("FullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("City")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("Conference")]
    public string Conference { get; set; } = string.Empty;

    [JsonPropertyName("Division")]
    public string Division { get; set; } = string.Empty;
}

// --- Players ---
// Endpoint: /scores/json/Players/{team}
// Response: flat array of player objects

public class SportsDataPlayerDto
{
    [JsonPropertyName("PlayerID")]
    public int PlayerId { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Team")]
    public string? Team { get; set; }

    [JsonPropertyName("Position")]
    public string Position { get; set; } = string.Empty;

    [JsonPropertyName("Number")]
    public int? Number { get; set; }

    [JsonPropertyName("Height")]
    public string? Height { get; set; }

    [JsonPropertyName("Weight")]
    public int? Weight { get; set; }

    [JsonPropertyName("College")]
    public string? College { get; set; }
}

// --- Games ---
// Endpoint: /scores/json/ScoresByWeek/{season}/{week}
// Response: flat array of game objects

public class SportsDataGameDto
{
    [JsonPropertyName("GameKey")]
    public string GameKey { get; set; } = string.Empty;

    [JsonPropertyName("Season")]
    public int Season { get; set; }

    [JsonPropertyName("Week")]
    public int Week { get; set; }

    [JsonPropertyName("Date")]
    public string? Date { get; set; }

    [JsonPropertyName("HomeTeam")]
    public string HomeTeam { get; set; } = string.Empty;

    [JsonPropertyName("AwayTeam")]
    public string AwayTeam { get; set; } = string.Empty;

    [JsonPropertyName("HomeScore")]
    public int? HomeScore { get; set; }

    [JsonPropertyName("AwayScore")]
    public int? AwayScore { get; set; }
}

// --- Player Game Stats ---
// Endpoint: /stats/json/PlayerGameStatsByWeek/{season}/{week}
// Response: flat array of stat objects

public class SportsDataPlayerStatsDto
{
    [JsonPropertyName("PlayerID")]
    public int PlayerId { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Team")]
    public string? Team { get; set; }

    [JsonPropertyName("GameKey")]
    public string? GameKey { get; set; }

    [JsonPropertyName("PassingCompletions")]
    public int PassingCompletions { get; set; }

    [JsonPropertyName("PassingAttempts")]
    public int PassingAttempts { get; set; }

    [JsonPropertyName("PassingYards")]
    public int PassingYards { get; set; }

    [JsonPropertyName("PassingTouchdowns")]
    public int PassingTouchdowns { get; set; }

    [JsonPropertyName("PassingInterceptions")]
    public int PassingInterceptions { get; set; }

    [JsonPropertyName("RushingAttempts")]
    public int RushingAttempts { get; set; }

    [JsonPropertyName("RushingYards")]
    public int RushingYards { get; set; }

    [JsonPropertyName("RushingTouchdowns")]
    public int RushingTouchdowns { get; set; }

    [JsonPropertyName("Receptions")]
    public int Receptions { get; set; }

    [JsonPropertyName("ReceivingYards")]
    public int ReceivingYards { get; set; }

    [JsonPropertyName("ReceivingTouchdowns")]
    public int ReceivingTouchdowns { get; set; }
}
