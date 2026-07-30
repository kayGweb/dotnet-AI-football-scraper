using System.ComponentModel;
using ModelContextProtocol.Server;

namespace WebScraper.Mcp.Tools;

/// <summary>
/// Data quality inspection and repair. These close the self-improvement loop:
/// findings are written automatically after every scrape job, and these tools let
/// an agent read them and enqueue idempotent re-scrapes to clear them.
/// </summary>
[McpServerToolType]
public static class QualityTools
{
    [McpServerTool(Name = "nfl_get_quality_findings"), Description(
        "List open data quality findings (missing stats, score mismatches, implausible values). " +
        "Each finding includes severity, the entity it concerns, and whether a repair job is already queued. " +
        "Requires operate scope.")]
    public static Task<string> GetQualityFindings(
        NflApiClient client,
        [Description("Max findings to return.")] int limit = 100,
        CancellationToken cancellationToken = default)
        => client.GetQualityFindingsAsync(limit, cancellationToken);

    [McpServerTool(Name = "nfl_scan_quality"), Description(
        "Run the quality rules engine and return the number of findings produced. " +
        "Scope it with season/seasonType/week, or omit all three to scan everything. " +
        "Requires operate scope.")]
    public static Task<string> ScanQuality(
        NflApiClient client,
        [Description("Limit scan to this season.")] int? season = null,
        [Description("preseason|regular|postseason")] string? seasonType = null,
        [Description("Limit scan to this week.")] int? week = null,
        CancellationToken cancellationToken = default)
        => client.ScanQualityAsync(season, seasonType, week, cancellationToken);

    [McpServerTool(Name = "nfl_repair_finding"), Description(
        "Enqueue a repair scrape job for one finding. Repairs are idempotent re-scrapes, " +
        "so this is safe to call more than once. Returns the new job id. Requires operate scope.")]
    public static Task<string> RepairFinding(
        NflApiClient client,
        [Description("Finding id from nfl_get_quality_findings.")] long findingId,
        CancellationToken cancellationToken = default)
        => client.RepairFindingAsync(findingId, cancellationToken);

    [McpServerTool(Name = "nfl_enqueue_repairs"), Description(
        "Enqueue repair jobs for all actionable open findings at once. Returns how many were queued. " +
        "Prefer this over repairing one finding at a time after a large backfill. Requires operate scope.")]
    public static Task<string> EnqueueRepairs(
        NflApiClient client,
        [Description("Max repair jobs to enqueue.")] int limit = 50,
        CancellationToken cancellationToken = default)
        => client.EnqueueRepairsAsync(limit, cancellationToken);

    [McpServerTool(Name = "nfl_refresh_coverage"), Description(
        "Recompute expected-vs-actual coverage snapshots and re-run quality rules. " +
        "Set enqueueRepairs=true to also queue repairs for whatever it finds. Requires operate scope.")]
    public static Task<string> RefreshCoverage(
        NflApiClient client,
        [Description("Limit refresh to this season.")] int? season = null,
        [Description("preseason|regular|postseason")] string? seasonType = null,
        [Description("Also enqueue repair jobs for open findings.")] bool enqueueRepairs = false,
        CancellationToken cancellationToken = default)
        => client.RefreshCoverageAsync(season, seasonType, enqueueRepairs, cancellationToken);
}
