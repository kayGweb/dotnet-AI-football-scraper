using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Services.Coverage;

/// <summary>
/// Enqueues idempotent re-scrape jobs for findings that map to repair actions.
/// </summary>
public class RepairJobEnqueuer
{
    private readonly AppDbContext _db;

    public RepairJobEnqueuer(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<int>> EnqueueRepairsForOpenFindingsAsync(
        int limit = 50,
        string? requestedBy = null,
        CancellationToken cancellationToken = default)
    {
        var findings = await _db.DataQualityFindings
            .Where(f => f.Status == DataQualityStatus.Open &&
                        f.Payload != null &&
                        (f.RuleType == DataQualityRuleType.GameMissingPlayerStats ||
                         f.RuleType == DataQualityRuleType.GameMissingTeamStats ||
                         f.RuleType == DataQualityRuleType.QuarterScoresMismatch ||
                         f.RuleType == DataQualityRuleType.WeekGameCountMismatch ||
                         f.RuleType == DataQualityRuleType.ImplausiblePassingYards))
            .OrderByDescending(f => f.Severity)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var enqueuedIds = new List<int>();

        foreach (var finding in findings)
        {
            var jobId = await TryEnqueueRepairAsync(finding, requestedBy, cancellationToken);
            if (jobId is null)
                continue;

            finding.Status = DataQualityStatus.RepairQueued;
            finding.RepairJobId = jobId;
            enqueuedIds.Add(jobId.Value);
        }

        if (enqueuedIds.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return enqueuedIds;
    }

    public async Task<int?> EnqueueRepairForFindingAsync(
        long findingId,
        string? requestedBy = null,
        CancellationToken cancellationToken = default)
    {
        var finding = await _db.DataQualityFindings.FindAsync(new object[] { findingId }, cancellationToken);
        if (finding is null || finding.Status is DataQualityStatus.Resolved or DataQualityStatus.Dismissed)
            return null;

        var jobId = await TryEnqueueRepairAsync(finding, requestedBy, cancellationToken);
        if (jobId is null)
            return null;

        finding.Status = DataQualityStatus.RepairQueued;
        finding.RepairJobId = jobId;
        await _db.SaveChangesAsync(cancellationToken);

        return jobId;
    }

    private async Task<int?> TryEnqueueRepairAsync(
        DataQualityFinding finding,
        string? requestedBy,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(finding.Payload))
            return null;

        using var doc = JsonDocument.Parse(finding.Payload);
        var root = doc.RootElement;

        var repairType = root.TryGetProperty("repairType", out var rt) ? rt.GetString() : null;
        if (repairType is not ("games" or "stats"))
            return null;

        var season = finding.Season ?? (root.TryGetProperty("season", out var s) ? s.GetInt32() : (int?)null);
        var week = finding.Week ?? (root.TryGetProperty("week", out var w) ? w.GetInt32() : (int?)null);
        var seasonType = finding.SeasonType ??
            (root.TryGetProperty("seasonType", out var st) && Enum.TryParse<NflSeasonType>(st.GetString(), out var parsed)
                ? parsed
                : NflSeasonType.Regular);

        if (season is null || week is null)
            return null;

        var jobType = repairType == "games" ? ScrapeJobType.Games : ScrapeJobType.Stats;

        var existing = await _db.ScrapeJobs
            .Where(j => j.Type == jobType &&
                        j.Season == season &&
                        j.SeasonType == seasonType &&
                        j.Week == week &&
                        (j.Status == ScrapeJobStatus.Queued || j.Status == ScrapeJobStatus.Running))
            .Select(j => j.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != 0)
            return existing;

        var job = new ScrapeJob
        {
            Type = jobType,
            Source = "Repair",
            Season = season,
            SeasonType = seasonType,
            Week = week,
            Status = ScrapeJobStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            RequestedBy = requestedBy ?? "quality-repair",
        };

        _db.ScrapeJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);

        return job.Id;
    }
}
