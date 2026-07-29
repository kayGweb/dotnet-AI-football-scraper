using WebScraper.Models;

namespace WebScraper.Services.Coverage;

/// <summary>Aggregate progress for a Backfill parent job and its child scrape jobs.</summary>
public sealed record BackfillProgress(
    int ParentJobId,
    string ParentStatus,
    int StartSeason,
    int EndSeason,
    int TotalChildJobs,
    int Succeeded,
    int Failed,
    int Running,
    int Queued,
    int Paused,
    double PercentComplete,
    TimeSpan? EstimatedRemaining);

public static class BackfillProgressCalculator
{
    public static BackfillProgress Compute(ScrapeJob parent, IReadOnlyList<ScrapeJob> children)
    {
        var startSeason = parent.Season ?? 0;
        var endSeason = parent.Week ?? startSeason;

        if (children.Count == 0)
        {
            return new BackfillProgress(
                parent.Id,
                parent.Status.ToString(),
                startSeason,
                endSeason,
                0, 0, 0, 0, 0, 0,
                0,
                null);
        }

        var succeeded = children.Count(c => c.Status == ScrapeJobStatus.Succeeded);
        var failed = children.Count(c => c.Status == ScrapeJobStatus.Failed);
        var running = children.Count(c => c.Status == ScrapeJobStatus.Running);
        var queued = children.Count(c => c.Status == ScrapeJobStatus.Queued);
        var paused = children.Count(c => c.Status == ScrapeJobStatus.Paused);
        var terminal = succeeded + failed;
        var percent = children.Count > 0 ? (double)terminal / children.Count * 100.0 : 0;

        TimeSpan? eta = null;
        if (parent.StartedAt is not null && terminal > 0 && terminal < children.Count)
        {
            var elapsed = DateTime.UtcNow - parent.StartedAt.Value;
            var rate = elapsed.TotalSeconds / terminal;
            var remaining = children.Count - terminal;
            eta = TimeSpan.FromSeconds(rate * remaining);
        }

        return new BackfillProgress(
            parent.Id,
            parent.Status.ToString(),
            startSeason,
            endSeason,
            children.Count,
            succeeded,
            failed,
            running,
            queued,
            paused,
            Math.Round(percent, 1),
            eta);
    }

    /// <summary>
    /// Child job IDs that are Queued and have satisfied dependencies (ready to run).
    /// </summary>
    public static IReadOnlyList<int> GetEnqueueableChildIds(IReadOnlyList<ScrapeJob> children)
    {
        var statusById = children.ToDictionary(c => c.Id, c => c.Status);
        var result = new List<int>();

        foreach (var child in children)
        {
            if (child.Status != ScrapeJobStatus.Queued)
                continue;

            if (child.DependsOnJobId is int depId)
            {
                if (!statusById.TryGetValue(depId, out var depStatus) || depStatus != ScrapeJobStatus.Succeeded)
                    continue;
            }

            result.Add(child.Id);
        }

        return result;
    }
}
