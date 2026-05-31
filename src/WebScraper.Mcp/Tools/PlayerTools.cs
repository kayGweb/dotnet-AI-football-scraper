using System.ComponentModel;
using ModelContextProtocol.Server;

namespace WebScraper.Mcp.Tools;

[McpServerToolType]
public static class PlayerTools
{
    [McpServerTool(Name = "nfl_list_players"), Description(
        "List NFL players with optional filters. Use teamAbbreviation (e.g. \"KC\") " +
        "to get a team roster, or position (e.g. \"QB\", \"WR\") to filter by " +
        "position. Returns a paged result.")]
    public static Task<string> ListPlayers(
        NflApiClient client,
        [Description("Filter by team primary key id.")] int? teamId = null,
        [Description("Filter by NFL team abbreviation (e.g. \"KC\"). Ignored if teamId is set.")] string? teamAbbreviation = null,
        [Description("Filter by position code (e.g. \"QB\", \"RB\", \"WR\", \"TE\", \"DE\").")] string? position = null,
        [Description("Page number, 1-based. Defaults to 1.")] int page = 1,
        [Description("Items per page, 1–200. Defaults to 25.")] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => client.ListPlayersAsync(teamId, teamAbbreviation, position, page, pageSize, cancellationToken);

    [McpServerTool(Name = "nfl_get_player"), Description(
        "Get a single player by primary key id, including their team abbreviation.")]
    public static Task<string> GetPlayer(
        NflApiClient client,
        [Description("Player primary key id (integer).")] int id,
        CancellationToken cancellationToken = default)
        => client.GetPlayerByIdAsync(id, cancellationToken);

    [McpServerTool(Name = "nfl_get_player_stats"), Description(
        "Get a player's per-game stats. Optionally filter by season and/or week. " +
        "Returns all 10 stat categories (passing, rushing, receiving, fumbles, " +
        "defensive, interceptions, kick returns, punt returns, kicking, punting).")]
    public static Task<string> GetPlayerStats(
        NflApiClient client,
        [Description("Player primary key id (integer).")] int id,
        [Description("Filter to a single NFL season, e.g. 2025.")] int? season = null,
        [Description("Filter to a single week, 1–22.")] int? week = null,
        CancellationToken cancellationToken = default)
        => client.GetPlayerStatsAsync(id, season, week, cancellationToken);
}
