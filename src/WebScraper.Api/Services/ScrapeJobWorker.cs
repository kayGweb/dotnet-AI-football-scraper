using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebScraper.Data;
using WebScraper.Models;
using WebScraper.Services.Coverage;
using WebScraper.Services.Scrapers;

namespace WebScraper.Api.Services;

/// <summary>
/// Background worker that dequeues job IDs from <see cref="IJobQueue"/> and runs
/// the matching scraper. Jobs are persisted before enqueue so the worker only
/// needs the ID. On startup, any Queued or Running rows left from a previous
/// crash are re-queued (scrapers are idempotent via upsert).
/// </summary>
public class ScrapeJobWorker : BackgroundService
{
    private readonly IJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScrapeJobWorker> _logger;

    public ScrapeJobWorker(
        IJobQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ScrapeJobWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverOrphanedJobs(stoppingToken);

        await foreach (var jobId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await RunJobAsync(jobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing ScrapeJob {JobId}", jobId);
            }
        }
    }

    private async Task RecoverOrphanedJobs(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var orphaned = await db.ScrapeJobs
            .Where(j => j.Status == ScrapeJobStatus.Queued || j.Status == ScrapeJobStatus.Running)
            .OrderBy(j => j.CreatedAt)
            .ToListAsync(ct);

        if (orphaned.Count == 0) return;

        _logger.LogInformation("Re-queuing {Count} orphaned scrape jobs from previous run", orphaned.Count);

        await db.ScrapeJobs
            .Where(j => j.Status == ScrapeJobStatus.Running)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, ScrapeJobStatus.Queued), ct);

        foreach (var job in orphaned)
        {
            if (await IsParentPausedAsync(db, job, ct))
                continue;

            if (await IsJobReadyToRunAsync(db, job, ct))
                _queue.TryEnqueue(job.Id);
        }
    }

    private static async Task<bool> IsParentPausedAsync(AppDbContext db, ScrapeJob job, CancellationToken ct)
    {
        if (job.ParentJobId is not int parentId)
            return false;

        var parentStatus = await db.ScrapeJobs
            .AsNoTracking()
            .Where(j => j.Id == parentId)
            .Select(j => j.Status)
            .FirstOrDefaultAsync(ct);

        return parentStatus == ScrapeJobStatus.Paused;
    }

    private static async Task<bool> IsJobReadyToRunAsync(AppDbContext db, ScrapeJob job, CancellationToken ct)
    {
        if (job.DependsOnJobId is not int depId)
            return true;

        var dep = await db.ScrapeJobs
            .AsNoTracking()
            .Where(j => j.Id == depId)
            .Select(j => j.Status)
            .FirstOrDefaultAsync(ct);

        return dep == ScrapeJobStatus.Succeeded;
    }

    private async Task EnqueueDependentJobsAsync(AppDbContext db, int completedJobId, CancellationToken ct)
    {
        var dependentIds = await db.ScrapeJobs
            .Where(j => j.DependsOnJobId == completedJobId && j.Status == ScrapeJobStatus.Queued)
            .Select(j => j.Id)
            .ToListAsync(ct);

        foreach (var id in dependentIds)
            _queue.TryEnqueue(id);
    }

    private async Task RunJobAsync(int jobId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var job = await db.ScrapeJobs.FindAsync(new object[] { jobId }, ct);
        if (job is null)
        {
            _logger.LogWarning("ScrapeJob {JobId} not found — skipping", jobId);
            return;
        }

        if (job.Status != ScrapeJobStatus.Queued)
        {
            _logger.LogInformation("ScrapeJob {JobId} is {Status}, not Queued — skipping", jobId, job.Status);
            return;
        }

        if (!await IsJobReadyToRunAsync(db, job, ct))
        {
            _logger.LogInformation(
                "ScrapeJob {JobId} waiting on dependency {DepId}",
                jobId, job.DependsOnJobId);
            return;
        }

        if (await IsParentPausedAsync(db, job, ct))
        {
            _logger.LogInformation("ScrapeJob {JobId} skipped — parent backfill is paused", jobId);
            return;
        }

        job.Status = ScrapeJobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        db.ScrapeEvents.Add(NewEvent(job, ScrapeEventType.JobStarted, new { startedAt = job.StartedAt }));
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Starting ScrapeJob {JobId}: {Type} (source={Source}, season={Season}, week={Week})",
            jobId, job.Type, job.Source, job.Season, job.Week);

        try
        {
            var result = await ExecuteScrapeAsync(scope.ServiceProvider, job, ct);

            if (job.Type == ScrapeJobType.Backfill)
            {
                job.Status = ScrapeJobStatus.Running;
                job.RecordsProcessed = result.RecordsProcessed;
                job.Error = result.Success ? null : result.Message;
            }
            else
            {
                job.Status = result.Success ? ScrapeJobStatus.Succeeded : ScrapeJobStatus.Failed;
                job.RecordsProcessed = result.RecordsProcessed;
                job.RecordsFailed = result.RecordsFailed;
                job.Error = result.Success ? null : result.Message;
                if (result.Errors.Count > 0)
                    job.Error = string.Join("; ", result.Errors);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ScrapeJob {JobId} threw an exception", jobId);
            job.Status = ScrapeJobStatus.Failed;
            job.Error = ex.Message;
        }

        job.CompletedAt = DateTime.UtcNow;

        if (job.Type == ScrapeJobType.Backfill && job.Status == ScrapeJobStatus.Running)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Backfill job {JobId} fan-out complete — {ChildCount} child jobs, parent stays Running",
                jobId, job.RecordsProcessed);
            return;
        }

        db.ScrapeEvents.Add(NewEvent(
            job,
            job.Status == ScrapeJobStatus.Succeeded ? ScrapeEventType.JobCompleted : ScrapeEventType.JobFailed,
            new
            {
                status = job.Status.ToString(),
                recordsProcessed = job.RecordsProcessed,
                recordsFailed = job.RecordsFailed,
                error = job.Error,
                completedAt = job.CompletedAt,
            }));
        await db.SaveChangesAsync(ct);

        await BackfillOrchestrator.TryCompleteParentAsync(db, job, ct);

        if (job.Status == ScrapeJobStatus.Succeeded)
        {
            await EnqueueDependentJobsAsync(db, jobId, ct);
            await RunPostScrapeQualityAsync(scope.ServiceProvider, job, ct);
        }

        _logger.LogInformation("ScrapeJob {JobId} finished: {Status} ({Processed} processed, {Failed} failed)",
            jobId, job.Status, job.RecordsProcessed, job.RecordsFailed);
    }

    private static ScrapeEvent NewEvent(ScrapeJob job, ScrapeEventType type, object payload) => new()
    {
        JobId = job.Id,
        EventType = type,
        Timestamp = DateTime.UtcNow,
        Payload = JsonSerializer.Serialize(payload),
    };

    private static async Task<ScrapeResult> ExecuteScrapeAsync(
        IServiceProvider services, ScrapeJob job, CancellationToken ct)
    {
        return job.Type switch
        {
            ScrapeJobType.Teams => await RunTeamsAsync(services),
            ScrapeJobType.Players => await RunPlayersAsync(services),
            ScrapeJobType.Games => await RunGamesAsync(services, job),
            ScrapeJobType.Stats => await RunStatsAsync(services, job),
            ScrapeJobType.All => await RunAllAsync(services, job),
            ScrapeJobType.Backfill => await RunBackfillAsync(services, job, ct),
            ScrapeJobType.OddsPoll => await RunOddsPollAsync(services, job, ct),
            _ => ScrapeResult.Failed($"Unknown job type: {job.Type}"),
        };
    }

    private static async Task<ScrapeResult> RunBackfillAsync(
        IServiceProvider services, ScrapeJob job, CancellationToken ct)
    {
        if (job.Season is null)
            return ScrapeResult.Failed("Start season is required for backfill (use Season field)");

        var endSeason = job.Week ?? job.Season.Value;
        var orchestrator = services.GetRequiredService<BackfillOrchestrator>();
        var queue = services.GetRequiredService<IJobQueue>();

        var childIds = await orchestrator.FanOutAsync(job, job.Season.Value, endSeason, ct);
        foreach (var childId in childIds.InitialEnqueueIds)
            queue.TryEnqueue(childId);

        return ScrapeResult.Succeeded(childIds.AllChildIds.Count, $"Enqueued {childIds.InitialEnqueueIds.Count} games jobs ({childIds.AllChildIds.Count} total child jobs)");
    }

    private static async Task<ScrapeResult> RunOddsPollAsync(
        IServiceProvider services, ScrapeJob job, CancellationToken ct)
    {
        var poll = services.GetRequiredService<IOddsPollService>();
        return await poll.PollAsync(job.Season, ct);
    }

    private static async Task RunPostScrapeQualityAsync(
        IServiceProvider services, ScrapeJob job, CancellationToken ct)
    {
        if (job.Type is not (ScrapeJobType.Games or ScrapeJobType.Stats or ScrapeJobType.All or ScrapeJobType.OddsPoll))
            return;

        var coverage = services.GetRequiredService<SeasonCoverageService>();
        var quality = services.GetRequiredService<QualityRulesEngine>();
        var repair = services.GetRequiredService<RepairJobEnqueuer>();
        var queue = services.GetRequiredService<IJobQueue>();

        await coverage.RefreshAsync(job.Season, job.SeasonType, ct);
        await quality.RunAsync(job.Season, job.SeasonType, job.Week, ct);

        var repairJobIds = await repair.EnqueueRepairsForOpenFindingsAsync(10, job.RequestedBy, ct);
        foreach (var id in repairJobIds)
            queue.TryEnqueue(id);
    }

    private static async Task<ScrapeResult> RunTeamsAsync(IServiceProvider services)
    {
        var scraper = services.GetRequiredService<ITeamScraperService>();
        return await scraper.ScrapeTeamsAsync();
    }

    private static async Task<ScrapeResult> RunPlayersAsync(IServiceProvider services)
    {
        var scraper = services.GetRequiredService<IPlayerScraperService>();
        return await scraper.ScrapeAllPlayersAsync();
    }

    private static async Task<ScrapeResult> RunGamesAsync(IServiceProvider services, ScrapeJob job)
    {
        var scraper = services.GetRequiredService<IGameScraperService>();
        if (job.Season is null)
            return ScrapeResult.Failed("Season is required for games scrape");

        var seasonType = job.SeasonType ?? NflSeasonType.Regular;

        return job.Week is not null
            ? await scraper.ScrapeGamesAsync(job.Season.Value, job.Week.Value, seasonType)
            : await scraper.ScrapeGamesAsync(job.Season.Value, seasonType);
    }

    private static async Task<ScrapeResult> RunStatsAsync(IServiceProvider services, ScrapeJob job)
    {
        var scraper = services.GetRequiredService<IStatsScraperService>();
        if (job.Season is null || job.Week is null)
            return ScrapeResult.Failed("Season and week are required for stats scrape");

        var seasonType = job.SeasonType ?? NflSeasonType.Regular;
        return await scraper.ScrapePlayerStatsAsync(job.Season.Value, job.Week.Value, seasonType);
    }

    private static async Task<ScrapeResult> RunAllAsync(IServiceProvider services, ScrapeJob job)
    {
        if (job.Season is null)
            return ScrapeResult.Failed("Season is required for full pipeline scrape");

        var totalProcessed = 0;
        var totalFailed = 0;
        var errors = new List<string>();

        var teamResult = await RunTeamsAsync(services);
        totalProcessed += teamResult.RecordsProcessed;
        totalFailed += teamResult.RecordsFailed;
        if (!teamResult.Success) errors.Add($"Teams: {teamResult.Message}");

        var playerResult = await RunPlayersAsync(services);
        totalProcessed += playerResult.RecordsProcessed;
        totalFailed += playerResult.RecordsFailed;
        if (!playerResult.Success) errors.Add($"Players: {playerResult.Message}");

        var gameResult = await RunGamesAsync(services, job);
        totalProcessed += gameResult.RecordsProcessed;
        totalFailed += gameResult.RecordsFailed;
        if (!gameResult.Success) errors.Add($"Games: {gameResult.Message}");

        if (job.Week is not null)
        {
            var statsResult = await RunStatsAsync(services, job);
            totalProcessed += statsResult.RecordsProcessed;
            totalFailed += statsResult.RecordsFailed;
            if (!statsResult.Success) errors.Add($"Stats: {statsResult.Message}");
        }

        return errors.Count > 0
            ? new ScrapeResult
            {
                Success = false,
                RecordsProcessed = totalProcessed,
                RecordsFailed = totalFailed,
                Message = "Partial failure in full pipeline",
                Errors = errors,
            }
            : ScrapeResult.Succeeded(totalProcessed, "Full pipeline completed");
    }
}
