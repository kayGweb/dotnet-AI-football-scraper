using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace WebScraper.Mcp.Tools;

[McpServerToolType]
public static class ProposeTools
{
    [McpServerTool(Name = "nfl_propose_correction"), Description(
        "Propose a field correction for human approval. Requires admin scope. " +
        "Never mutate data directly — always propose with rationale.")]
    public static Task<string> ProposeCorrection(
        NflApiClient client,
        [Description("Entity type: Player, Team, Game.")] string entityType,
        [Description("Entity primary key.")] int id,
        [Description("Field name to correct.")] string field,
        [Description("Proposed new value.")] string newValue,
        [Description("Why this change is correct (include source).")] string rationale,
        CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.Serialize(new
        {
            entityType,
            entityId = id,
            field,
            newValue,
            rationale,
        });
        return client.ProposeCorrectionAsync(body, cancellationToken);
    }

    [McpServerTool(Name = "nfl_list_corrections"), Description(
        "List agent-proposed corrections. Filter by status: Pending, Approved, Rejected, Applied.")]
    public static Task<string> ListCorrections(
        NflApiClient client,
        [Description("Status filter.")] string? status = null,
        CancellationToken cancellationToken = default)
        => client.ListCorrectionsAsync(status, cancellationToken);
}
