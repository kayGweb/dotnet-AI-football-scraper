using System.ComponentModel;
using ModelContextProtocol.Server;

namespace WebScraper.Mcp.Tools;

[McpServerToolType]
public static class StatusTools
{
    [McpServerTool(Name = "nfl_get_status"), Description(
        "Get a snapshot of the NFL database: record counts for teams, players, " +
        "games, player game stats, venues, team game stats, injuries, and API " +
        "links, plus the freshest UpdatedAt timestamp across the main tables. " +
        "Useful for sanity-checking before answering questions about a season or " +
        "week (returns 0 counts when data hasn't been scraped yet).")]
    public static Task<string> GetStatus(
        NflApiClient client,
        CancellationToken cancellationToken = default)
        => client.GetStatusAsync(cancellationToken);
}
