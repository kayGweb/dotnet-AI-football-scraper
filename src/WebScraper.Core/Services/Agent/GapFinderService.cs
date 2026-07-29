using Microsoft.EntityFrameworkCore;
using WebScraper.Data;
using WebScraper.Models;
using WebScraper.Services.Coverage;

namespace WebScraper.Services.Agent;

/// <summary>
/// Ranks missing or suspect data for agent consumption.
/// </summary>
public class GapFinderService
{
    private readonly AppDbContext _db;

    public GapFinderService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<GapItem>> FindGapsAsync(int limit = 50, CancellationToken ct = default)
    {
        var gaps = new List<GapItem>();

        var findings = await _db.DataQualityFindings
            .AsNoTracking()
            .Where(f => f.Status == DataQualityStatus.Open)
            .OrderByDescending(f => f.Severity)
            .Take(limit)
            .ToListAsync(ct);

        gaps.AddRange(findings.Select(f => new GapItem
        {
            Kind = "quality",
            Severity = f.Severity.ToString(),
            Season = f.Season,
            SeasonType = f.SeasonType,
            Week = f.Week,
            Message = f.Message,
            EntityType = f.EntityType,
            EntityId = f.EntityId,
            FindingId = f.Id,
        }));

        var coverageGaps = await _db.SeasonCoverages
            .AsNoTracking()
            .Where(c => c.ExpectedGames != null && c.ActualGames < c.ExpectedGames)
            .OrderByDescending(c => c.Season)
            .Take(limit)
            .ToListAsync(ct);

        gaps.AddRange(coverageGaps.Select(c => new GapItem
        {
            Kind = "coverage",
            Severity = DataQualitySeverity.Error.ToString(),
            Season = c.Season,
            SeasonType = c.SeasonType,
            Week = c.Week,
            Message = $"{c.Season} {c.SeasonType} week {c.Week}: {c.ActualGames}/{c.ExpectedGames} games loaded",
        }));

        return gaps
            .OrderByDescending(g => g.Severity == DataQualitySeverity.Error.ToString())
            .ThenByDescending(g => g.Season)
            .Take(limit)
            .ToList();
    }
}

public class GapItem
{
    public string Kind { get; set; } = "";
    public string Severity { get; set; } = "";
    public int? Season { get; set; }
    public NflSeasonType? SeasonType { get; set; }
    public int? Week { get; set; }
    public string Message { get; set; } = "";
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public long? FindingId { get; set; }
}
