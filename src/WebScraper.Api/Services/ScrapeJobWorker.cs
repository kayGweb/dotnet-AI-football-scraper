using Microsoft.EntityFrameworkCore;
using WebScraper.Data;
using WebScraper.Models;
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
            .Select(j => j.Id)
            .ToListAsync(ct);

        if (orphaned.Count == 0) return;

        _logger.LogInformation("Re-queuing {Count} orphaned scrape jobs from previous run", orphaned.Count);

        await db.ScrapeJobs
            .Where(j => j.Status == ScrapeJobStatus.Running)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, ScrapeJobStatus.Queued), ct);

        foreach (var id in orphaned)
        {
            _queue.TryEnqueue(id);
        }
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

        job.Status = ScrapeJobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Starting ScrapeJob {JobId}: {Type} (source={Source}, season={Season}, week={Week})",
            jobId, job.Type, job.Source, job.Season, job.Week);

        try
        {
            var result = await ExecuteScrapeAsync(scope.ServiceProvider, job, ct);

            job.Status = result.Success ? ScrapeJobStatus.Succeeded : ScrapeJobStatus.Failed;
            job.RecordsProcessed = result.RecordsProcessed;
            job.RecordsFailed = result.RecordsFailed;
            job.Error = result.Success ? null : result.Message;
            if (result.Errors.Count > 0)
            {
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
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("ScrapeJob {JobId} finished: {Status} ({Processed} processed, {Failed} failed)",
            jobId, job.Status, job.RecordsProcessed, job.RecordsFailed);
    }

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
            _ => ScrapeResult.Failed($"Unknown job type: {job.Type}"),
        };
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

        return job.Week is not null
            ? await scraper.ScrapeGamesAsync(job.Season.Value, job.Week.Value)
            : await scraper.ScrapeGamesAsync(job.Season.Value);
    }

    private static async Task<ScrapeResult> RunStatsAsync(IServiceProvider services, ScrapeJob job)
    {
        var scraper = services.GetRequiredService<IStatsScraperService>();
        if (job.Season is null || job.Week is null)
            return ScrapeResult.Failed("Season and week are required for stats scrape");

        return await scraper.ScrapePlayerStatsAsync(job.Season.Value, job.Week.Value);
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
