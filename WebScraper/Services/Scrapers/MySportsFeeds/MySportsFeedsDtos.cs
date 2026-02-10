using System.Text.Json.Serialization;

namespace WebScraper.Services.Scrapers.MySportsFeeds;

// --- Teams ---
// Endpoint: /{season}/teams.json
// Response: { "teams": [ { "team": { ... } } ] }

public class MySportsFeedsTeamsResponse
{
    [JsonPropertyName("teams")]
    public List<MySportsFeedsTeamWrapper> Teams { get; set; } = new();
}

public class MySportsFeedsTeamWrapper
{
    [JsonPropertyName("team")]
    public MySportsFeedsTeam Team { get; set; } = new();
}

public class MySportsFeedsTeam
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("conference")]
    public string? Conference { get; set; }

    [JsonPropertyName("division")]
    public string? Division { get; set; }
}

// --- Players ---
// Endpoint: /players.json?team={abbr}&season={season}
// Response: { "players": [ { "player": { ... } } ] }

public class MySportsFeedsPlayersResponse
{
    [JsonPropertyName("players")]
    public List<MySportsFeedsPlayerWrapper> Players { get; set; } = new();
}

public class MySportsFeedsPlayerWrapper
{
    [JsonPropertyName("player")]
    public MySportsFeedsPlayer Player { get; set; } = new();
}

public class MySportsFeedsPlayer
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("position")]
    public string? Position { get; set; }

    [JsonPropertyName("jerseyNumber")]
    public int? JerseyNumber { get; set; }

    [JsonPropertyName("height")]
    public string? Height { get; set; }

    [JsonPropertyName("weight")]
    public int? Weight { get; set; }

    [JsonPropertyName("college")]
    public string? College { get; set; }

    [JsonPropertyName("currentTeam")]
    public MySportsFeedsCurrentTeam? CurrentTeam { get; set; }
}

public class MySportsFeedsCurrentTeam
{
    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;
}

// --- Games ---
// Endpoint: /{season}/games.json?week={n}
// Response: { "games": [ { "schedule": { ... } } ] }

public class MySportsFeedsGamesResponse
{
    [JsonPropertyName("games")]
    public List<MySportsFeedsGameWrapper> Games { get; set; } = new();
}

public class MySportsFeedsGameWrapper
{
    [JsonPropertyName("schedule")]
    public MySportsFeedsSchedule Schedule { get; set; } = new();
}

public class MySportsFeedsSchedule
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("week")]
    public int Week { get; set; }

    [JsonPropertyName("startTime")]
    public string? StartTime { get; set; }

    [JsonPropertyName("homeTeam")]
    public MySportsFeedsGameTeam HomeTeam { get; set; } = new();

    [JsonPropertyName("awayTeam")]
    public MySportsFeedsGameTeam AwayTeam { get; set; } = new();

    [JsonPropertyName("score")]
    public MySportsFeedsScore? Score { get; set; }
}

public class MySportsFeedsGameTeam
{
    [JsonPropertyName("abbreviation")]
    public string Abbreviation { get; set; } = string.Empty;
}

public class MySportsFeedsScore
{
    [JsonPropertyName("homeScoreTotal")]
    public int? HomeScoreTotal { get; set; }

    [JsonPropertyName("awayScoreTotal")]
    public int? AwayScoreTotal { get; set; }
}

// --- Player Game Logs (Stats) ---
// Endpoint: /{season}/week/{week}/player_gamelogs.json
// Response: { "gamelogs": [ { "player": { ... }, "game": { ... }, "stats": { ... } } ] }

public class MySportsFeedsGameLogsResponse
{
    [JsonPropertyName("gamelogs")]
    public List<MySportsFeedsGameLog> Gamelogs { get; set; } = new();
}

public class MySportsFeedsGameLog
{
    [JsonPropertyName("player")]
    public MySportsFeedsGameLogPlayer Player { get; set; } = new();

    [JsonPropertyName("game")]
    public MySportsFeedsGameLogGame Game { get; set; } = new();

    [JsonPropertyName("stats")]
    public MySportsFeedsStats Stats { get; set; } = new();
}

public class MySportsFeedsGameLogPlayer
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;
}

public class MySportsFeedsGameLogGame
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("week")]
    public int Week { get; set; }

    [JsonPropertyName("homeTeam")]
    public MySportsFeedsGameTeam? HomeTeam { get; set; }

    [JsonPropertyName("awayTeam")]
    public MySportsFeedsGameTeam? AwayTeam { get; set; }
}

public class MySportsFeedsStats
{
    [JsonPropertyName("passing")]
    public MySportsFeedsPassingStats? Passing { get; set; }

    [JsonPropertyName("rushing")]
    public MySportsFeedsRushingStats? Rushing { get; set; }

    [JsonPropertyName("receiving")]
    public MySportsFeedsReceivingStats? Receiving { get; set; }
}

public class MySportsFeedsPassingStats
{
    [JsonPropertyName("passCompletions")]
    public int PassCompletions { get; set; }

    [JsonPropertyName("passAttempts")]
    public int PassAttempts { get; set; }

    [JsonPropertyName("passYards")]
    public int PassYards { get; set; }

    [JsonPropertyName("passTD")]
    public int PassTouchdowns { get; set; }

    [JsonPropertyName("passInt")]
    public int Interceptions { get; set; }
}

public class MySportsFeedsRushingStats
{
    [JsonPropertyName("rushAttempts")]
    public int RushAttempts { get; set; }

    [JsonPropertyName("rushYards")]
    public int RushYards { get; set; }

    [JsonPropertyName("rushTD")]
    public int RushTouchdowns { get; set; }
}

public class MySportsFeedsReceivingStats
{
    [JsonPropertyName("receptions")]
    public int Receptions { get; set; }

    [JsonPropertyName("recYards")]
    public int ReceivingYards { get; set; }

    [JsonPropertyName("recTD")]
    public int ReceivingTouchdowns { get; set; }
}
