using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace WebScraper.Mcp.Tools;

[McpServerToolType]
public static class IntrospectTools
{
    [McpServerTool(Name = "nfl_describe_schema"), Description(
        "Describe database entities, columns, and relationships. Omit entity for catalog.")]
    public static Task<string> DescribeSchema(
        NflApiClient client,
        [Description("Entity name: Team, Player, Game, TeamSeason, etc.")] string? entity = null,
        CancellationToken cancellationToken = default)
        => client.DescribeSchemaAsync(entity, cancellationToken);

    [McpServerTool(Name = "nfl_get_data_dictionary"), Description(
        "Stat column meanings, seasonType values, and agent rules of thumb.")]
    public static Task<string> GetDataDictionary(
        NflApiClient client,
        CancellationToken cancellationToken = default)
        => client.GetDataDictionaryAsync(cancellationToken);

    [McpServerTool(Name = "nfl_query_stats"), Description(
        "Parameterized aggregation over player_game_stats. No raw SQL. " +
        "Pass a JSON body with dataset, measure, aggregation, groupBy, filters, limit.")]
    public static Task<string> QueryStats(
        NflApiClient client,
        [Description("JSON QueryStatsRequest body.")] string requestJson,
        CancellationToken cancellationToken = default)
        => client.QueryStatsAsync(requestJson, cancellationToken);
}
