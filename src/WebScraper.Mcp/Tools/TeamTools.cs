using System.ComponentModel;
using ModelContextProtocol.Server;

namespace WebScraper.Mcp.Tools;

/// <summary>
/// MCP tools for NFL teams. Tool names are prefixed with <c>nfl_</c> so they
/// remain unambiguous when Claude has multiple MCP servers attached.
/// </summary>
[McpServerToolType]
public static class TeamTools
{
    [McpServerTool(Name = "nfl_list_teams"), Description(
        "List NFL teams. Returns a paged result with team summaries (id, name, " +
        "abbreviation, city, conference, division). Use the optional conference " +
        "filter to narrow to AFC or NFC.")]
    public static Task<string> ListTeams(
        NflApiClient client,
        [Description("Optional conference filter — \"AFC\" or \"NFC\".")] string? conference = null,
        [Description("Page number, 1-based. Defaults to 1.")] int page = 1,
        [Description("Items per page, 1–200. Defaults to 25.")] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => client.ListTeamsAsync(conference, page, pageSize, cancellationToken);

    [McpServerTool(Name = "nfl_get_team"), Description(
        "Get a single team by its primary key id.")]
    public static Task<string> GetTeam(
        NflApiClient client,
        [Description("Team primary key id (integer).")] int id,
        CancellationToken cancellationToken = default)
        => client.GetTeamByIdAsync(id, cancellationToken);

    [McpServerTool(Name = "nfl_get_team_by_abbreviation"), Description(
        "Get a single team by its NFL abbreviation (e.g. KC, SF, DAL).")]
    public static Task<string> GetTeamByAbbreviation(
        NflApiClient client,
        [Description("NFL team abbreviation, e.g. \"KC\" or \"DAL\".")] string abbreviation,
        CancellationToken cancellationToken = default)
        => client.GetTeamByAbbreviationAsync(abbreviation, cancellationToken);
}
