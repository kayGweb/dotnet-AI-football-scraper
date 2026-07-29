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

    public async Task<IReadOnlyList<int>> FanOutAsync(
        ScrapeJob parentJob,
        int startSeason,
        int endSeason,
        CancellationToken cancellationToken = default)
    {
        var workItems = BackfillPlanner.Plan(startSeason, endSeason);
        var childIds = new List<int>(workItems.Count);

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
                Status = ScrapeJobStatus.Queued,
                CreatedAt = DateTime.UtcNow,
                RequestedBy = parentJob.RequestedBy,
            };

            _db.ScrapeJobs.Add(child);
            await _db.SaveChangesAsync(cancellationToken);
            childIds.Add(child.Id);
        }

        return childIds;
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
