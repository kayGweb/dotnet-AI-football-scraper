using System.ComponentModel;
using ModelContextProtocol.Server;

namespace WebScraper.Mcp.Tools;

[McpServerToolType]
public static class GameTools
{
    [McpServerTool(Name = "nfl_list_games"), Description(
        "List NFL games with optional filters for season, week, and team. The " +
        "teamId filter matches either home or away. Returns a paged result with " +
        "home/away teams, scores, venue, and quarter scores.")]
    public static Task<string> ListGames(
        NflApiClient client,
        [Description("Filter to a single NFL season, e.g. 2025.")] int? season = null,
        [Description("Filter to a single week, 1–22.")] int? week = null,
        [Description("Filter to games involving this team (home OR away).")] int? teamId = null,
        [Description("Page number, 1-based. Defaults to 1.")] int page = 1,
        [Description("Items per page, 1–200. Defaults to 25.")] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => client.ListGamesAsync(season, week, teamId, page, pageSize, cancellationToken);

    [McpServerTool(Name = "nfl_get_game"), Description(
        "Get a single game with teams, venue, quarter scores, and ESPN metadata.")]
    public static Task<string> GetGame(
        NflApiClient client,
        [Description("Game primary key id (integer).")] int id,
        CancellationToken cancellationToken = default)
        => client.GetGameByIdAsync(id, cancellationToken);

    [McpServerTool(Name = "nfl_get_game_team_stats"), Description(
        "Get team-level per-game stats for a game (home + away rows). Includes " +
        "first downs, total yards, turnovers, penalties, and possession time.")]
    public static Task<string> GetGameTeamStats(
        NflApiClient client,
        [Description("Game primary key id (integer).")] int id,
        CancellationToken cancellationToken = default)
        => client.GetGameTeamStatsAsync(id, cancellationToken);

    [McpServerTool(Name = "nfl_get_game_player_stats"), Description(
        "Get all player-level stat lines for a game (every player who recorded " +
        "any stat, across all 10 categories).")]
    public static Task<string> GetGamePlayerStats(
        NflApiClient client,
        [Description("Game primary key id (integer).")] int id,
        CancellationToken cancellationToken = default)
        => client.GetGamePlayerStatsAsync(id, cancellationToken);

    [McpServerTool(Name = "nfl_get_game_injuries"), Description(
        "Get all injury reports filed for a given game (status, injury type, body " +
        "location, expected return date).")]
    public static Task<string> GetGameInjuries(
        NflApiClient client,
        [Description("Game primary key id (integer).")] int id,
        CancellationToken cancellationToken = default)
        => client.GetGameInjuriesAsync(id, cancellationToken);

    [McpServerTool(Name = "nfl_get_game_drives"), Description(
        "Get all drives for a game (sequence, team, yards, result, scoring context).")]
    public static Task<string> GetGameDrives(
        NflApiClient client,
        [Description("Game primary key id (integer).")] int id,
        CancellationToken cancellationToken = default)
        => client.GetGameDrivesAsync(id, cancellationToken);

    [McpServerTool(Name = "nfl_get_game_scoring_plays"), Description(
        "Get all scoring plays for a game (period, clock, description, running score).")]
    public static Task<string> GetGameScoringPlays(
        NflApiClient client,
        [Description("Game primary key id (integer).")] int id,
        CancellationToken cancellationToken = default)
        => client.GetGameScoringPlaysAsync(id, cancellationToken);

    [McpServerTool(Name = "nfl_get_game_weather"), Description(
        "Get game-day weather conditions (temperature, wind, humidity).")]
    public static Task<string> GetGameWeather(
        NflApiClient client,
        [Description("Game primary key id (integer).")] int id,
        CancellationToken cancellationToken = default)
        => client.GetGameWeatherAsync(id, cancellationToken);

    [McpServerTool(Name = "nfl_get_game_officials"), Description(
        "Get the referee crew for a game (name and position).")]
    public static Task<string> GetGameOfficials(
        NflApiClient client,
        [Description("Game primary key id (integer).")] int id,
        CancellationToken cancellationToken = default)
        => client.GetGameOfficialsAsync(id, cancellationToken);

    [McpServerTool(Name = "nfl_get_game_odds"), Description(
        "Get betting odds snapshots for a game (spread, over/under, moneylines by sportsbook).")]
    public static Task<string> GetGameOdds(
        NflApiClient client,
        [Description("Game primary key id (integer).")] int id,
        CancellationToken cancellationToken = default)
        => client.GetGameOddsAsync(id, cancellationToken);
}
