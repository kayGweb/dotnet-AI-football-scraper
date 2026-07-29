using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace WebScraper.Mcp.Tools;

[McpServerToolType]
public static class OperateTools
{
    [McpServerTool(Name = "nfl_trigger_scrape"), Description(
        "Trigger a scrape job. Types: teams, players, games, stats, all, backfill, odds-poll. " +
        "Returns job id (202 Accepted). Requires operate scope.")]
    public static Task<string> TriggerScrape(
        NflApiClient client,
        [Description("Job type: teams|players|games|stats|all|backfill|odds-poll")] string type,
        [Description("NFL season year.")] int? season = null,
        [Description("Week number (or end season for backfill when endSeason omitted).")] int? week = null,
        [Description("preseason|regular|postseason")] string? seasonType = null,
        [Description("For backfill: end season year.")] int? endSeason = null,
        CancellationToken cancellationToken = default)
        => client.TriggerScrapeAsync(type, season, week, seasonType, endSeason, cancellationToken);

    [McpServerTool(Name = "nfl_get_job"), Description("Get scrape job status, progress, and errors.")]
    public static Task<string> GetJob(
        NflApiClient client,
        [Description("Scrape job id.")] int jobId,
        CancellationToken cancellationToken = default)
        => client.GetJobAsync(jobId, cancellationToken);

    [McpServerTool(Name = "nfl_list_jobs"), Description("List scrape jobs with optional status filter.")]
    public static Task<string> ListJobs(
        NflApiClient client,
        [Description("Filter: Queued, Running, Succeeded, Failed.")] string? status = null,
        [Description("Page number.")] int page = 1,
        [Description("Page size.")] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => client.ListJobsAsync(status, page, pageSize, cancellationToken);

    [McpServerTool(Name = "nfl_get_coverage"), Description(
        "Get expected-vs-actual coverage per week. Call before league-wide analytics.")]
    public static Task<string> GetCoverage(
        NflApiClient client,
        [Description("Filter to season.")] int? season = null,
        [Description("preseason|regular|postseason")] string? seasonType = null,
        CancellationToken cancellationToken = default)
        => client.GetCoverageAsync(season, seasonType, cancellationToken);

    [McpServerTool(Name = "nfl_find_gaps"), Description(
        "Ranked list of missing or suspect data from quality rules and coverage gaps.")]
    public static Task<string> FindGaps(
        NflApiClient client,
        [Description("Max items to return.")] int limit = 50,
        CancellationToken cancellationToken = default)
        => client.FindGapsAsync(limit, cancellationToken);

    [McpServerTool(Name = "nfl_retry_job"), Description("Re-queue a completed or failed scrape job.")]
    public static Task<string> RetryJob(
        NflApiClient client,
        [Description("Scrape job id.")] int jobId,
        CancellationToken cancellationToken = default)
        => client.RetryJobAsync(jobId, cancellationToken);

    [McpServerTool(Name = "nfl_get_backfill_progress"), Description(
        "Get aggregate progress for a backfill parent job (child counts, percent complete, ETA).")]
    public static Task<string> GetBackfillProgress(
        NflApiClient client,
        [Description("Backfill parent job id.")] int jobId,
        CancellationToken cancellationToken = default)
        => client.GetBackfillProgressAsync(jobId, cancellationToken);

    [McpServerTool(Name = "nfl_pause_backfill"), Description("Pause a running backfill — queued children stop dequeuing.")]
    public static Task<string> PauseBackfill(
        NflApiClient client,
        [Description("Backfill parent job id.")] int jobId,
        CancellationToken cancellationToken = default)
        => client.PauseBackfillAsync(jobId, cancellationToken);

    [McpServerTool(Name = "nfl_resume_backfill"), Description("Resume a paused backfill and re-enqueue ready child jobs.")]
    public static Task<string> ResumeBackfill(
        NflApiClient client,
        [Description("Backfill parent job id.")] int jobId,
        CancellationToken cancellationToken = default)
        => client.ResumeBackfillAsync(jobId, cancellationToken);

    [McpServerTool(Name = "nfl_get_push_status"), Description("Get the latest SQLite → PostgreSQL push session checkpoint.")]
    public static Task<string> GetPushStatus(
        NflApiClient client,
        CancellationToken cancellationToken = default)
        => client.GetPushStatusAsync(cancellationToken);

    [McpServerTool(Name = "nfl_trigger_push"), Description(
        "Push local SQLite data to PostgreSQL. Use resume=true to continue, reset=true to start fresh. Requires admin scope.")]
    public static Task<string> TriggerPush(
        NflApiClient client,
        [Description("Continue an interrupted push.")] bool resume = false,
        [Description("Clear session and start fresh.")] bool reset = false,
        CancellationToken cancellationToken = default)
        => client.TriggerPushAsync(resume, reset, cancellationToken);

    [McpServerTool(Name = "nfl_backup_database"), Description(
        "Create a timestamped backup of the local SQLite database (keeps last 3). Requires admin scope.")]
    public static Task<string> BackupDatabase(
        NflApiClient client,
        CancellationToken cancellationToken = default)
        => client.CreateBackupAsync(cancellationToken);
}
