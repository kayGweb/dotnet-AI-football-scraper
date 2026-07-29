using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Services.Coverage;

/// <summary>
/// Fans a Backfill parent job out into child ScrapeJob rows (games then stats per week).
/// </summary>
public class BackfillOrchestrator
{
    private readonly AppDbContext _db;
    private readonly ILogger<BackfillOrchestrator> _logger;

    public BackfillOrchestrator(AppDbContext db, ILogger<BackfillOrchestrator> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<BackfillFanOutResult> FanOutAsync(
        ScrapeJob parentJob,
        int startSeason,
        int endSeason,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.ScrapeJobs
            .Where(j => j.ParentJobId == parentJob.Id)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _logger.LogInformation(
                "Backfill job {ParentId} already has {Count} children — resuming fan-out",
                parentJob.Id, existing.Count);

            var enqueueIds = BackfillProgressCalculator.GetEnqueueableChildIds(existing);
            return new BackfillFanOutResult(
                existing.Select(c => c.Id).ToList(),
                enqueueIds);
        }

        var workItems = BackfillPlanner.Plan(startSeason, endSeason);
        var childIds = new List<int>(workItems.Count);
        var initialEnqueueIds = new List<int>();
        int? pendingGamesJobId = null;

        _logger.LogInformation(
            "Fanning out backfill job {ParentId}: {Count} child jobs for seasons {Start}-{End}",
            parentJob.Id, workItems.Count, startSeason, endSeason);

        foreach (var item in workItems)
        {
            var child = new ScrapeJob
            {
                Type = item.JobType,
                Source = parentJob.Source,
                Season = item.Season,
                SeasonType = item.SeasonType,
                Week = item.Week,
                ParentJobId = parentJob.Id,
                DependsOnJobId = item.JobType == ScrapeJobType.Stats ? pendingGamesJobId : null,
                Status = ScrapeJobStatus.Queued,
                CreatedAt = DateTime.UtcNow,
                RequestedBy = parentJob.RequestedBy,
            };

            _db.ScrapeJobs.Add(child);
            await _db.SaveChangesAsync(cancellationToken);
            childIds.Add(child.Id);

            if (item.JobType == ScrapeJobType.Games)
            {
                pendingGamesJobId = child.Id;
                initialEnqueueIds.Add(child.Id);
            }
            else
            {
                pendingGamesJobId = null;
            }
        }

        return new BackfillFanOutResult(childIds, initialEnqueueIds);
    }

    public async Task<BackfillProgress> GetProgressAsync(int parentJobId, CancellationToken ct = default)
    {
        var parent = await _db.ScrapeJobs.FindAsync(new object[] { parentJobId }, ct)
            ?? throw new KeyNotFoundException($"Backfill job {parentJobId} not found.");

        if (parent.Type != ScrapeJobType.Backfill)
            throw new InvalidOperationException($"Job {parentJobId} is not a Backfill job.");

        var children = await _db.ScrapeJobs
            .AsNoTracking()
            .Where(j => j.ParentJobId == parentJobId)
            .ToListAsync(ct);

        return BackfillProgressCalculator.Compute(parent, children);
    }

    public static async Task TryCompleteParentAsync(AppDbContext db, ScrapeJob completedChild, CancellationToken ct = default)
    {
        if (completedChild.ParentJobId is not int parentId)
            return;

        var parent = await db.ScrapeJobs.FindAsync(new object[] { parentId }, ct);
        if (parent is null || parent.Type != ScrapeJobType.Backfill)
            return;

        var children = await db.ScrapeJobs
            .Where(j => j.ParentJobId == parentId)
            .ToListAsync(ct);

        if (children.Count == 0)
            return;

        if (children.Any(c => c.Status is ScrapeJobStatus.Queued or ScrapeJobStatus.Running))
            return;

        parent.Status = children.All(c => c.Status == ScrapeJobStatus.Succeeded)
            ? ScrapeJobStatus.Succeeded
            : ScrapeJobStatus.Failed;
        parent.RecordsProcessed = children.Sum(c => c.RecordsProcessed);
        parent.RecordsFailed = children.Sum(c => c.RecordsFailed);
        parent.CompletedAt = DateTime.UtcNow;
        parent.Error = children.Any(c => c.Status == ScrapeJobStatus.Failed)
            ? $"{children.Count(c => c.Status == ScrapeJobStatus.Failed)} child jobs failed"
            : null;

        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Result of fanning out a backfill parent into child scrape jobs.</summary>
public sealed record BackfillFanOutResult(
    IReadOnlyList<int> AllChildIds,
    IReadOnlyList<int> InitialEnqueueIds);
