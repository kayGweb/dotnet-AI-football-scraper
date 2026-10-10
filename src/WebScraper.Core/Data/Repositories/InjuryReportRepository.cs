using Microsoft.EntityFrameworkCore;
using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public class InjuryReportRepository : IInjuryReportRepository
{
    private readonly AppDbContext _context;

    public InjuryReportRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<InjuryReport?> GetByIdAsync(int id)
        => await _context.InjuryReports
            .Include(r => r.TeamSeason)
            .ThenInclude(ts => ts.Franchise)
            .Include(r => r.Player)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<IEnumerable<InjuryReport>> GetAllAsync()
        => await _context.InjuryReports
            .Include(r => r.TeamSeason)
            .Include(r => r.Player)
            .ToListAsync();

    public async Task<InjuryReport> AddAsync(InjuryReport entity)
    {
        await _context.InjuryReports.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(InjuryReport entity)
    {
        _context.InjuryReports.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var report = await _context.InjuryReports.FindAsync(id);
        if (report != null)
        {
            _context.InjuryReports.Remove(report);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _context.InjuryReports.AnyAsync(r => r.Id == id);

    public async Task<InjuryReport> UpsertAsync(InjuryReport report)
    {
        var existing = await _context.InjuryReports
            .FirstOrDefaultAsync(r =>
                r.ExternalPlayerId == report.ExternalPlayerId &&
                r.SnapshotAt == report.SnapshotAt);

        if (existing != null)
        {
            ApplyFields(existing, report);
            _context.InjuryReports.Update(existing);
            await _context.SaveChangesAsync();
            return existing;
        }

        await _context.InjuryReports.AddAsync(report);
        await _context.SaveChangesAsync();
        return report;
    }

    public async Task<IReadOnlyList<InjuryReport>> GetCurrentAsync(string? teamAbbreviation = null)
    {
        var query = _context.InjuryReports
            .Include(r => r.TeamSeason)
            .ThenInclude(ts => ts.Franchise)
            .Include(r => r.Player)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(teamAbbreviation))
        {
            var abbr = teamAbbreviation.Trim();
            query = query.Where(r =>
                r.TeamSeason.Abbreviation == abbr ||
                r.TeamSeason.Franchise.CanonicalAbbreviation == abbr);
        }

        var rows = await query.ToListAsync();

        return rows
            .GroupBy(r => r.ExternalPlayerId)
            .Select(g => g.OrderByDescending(r => r.SnapshotAt).First())
            .OrderBy(r => r.PlayerName)
            .ToList();
    }

    private static void ApplyFields(InjuryReport target, InjuryReport source)
    {
        target.TeamSeasonId = source.TeamSeasonId;
        target.PlayerId = source.PlayerId;
        target.PlayerName = source.PlayerName;
        target.Position = source.Position;
        target.Status = source.Status;
        target.Description = source.Description;
        target.ReportedAt = source.ReportedAt;
        if (!string.IsNullOrEmpty(source.DataSource))
            target.DataSource = source.DataSource;
        if (source.DataSourceFetchedAt.HasValue)
            target.DataSourceFetchedAt = source.DataSourceFetchedAt;
        if (!string.IsNullOrEmpty(source.DataSourceRecordId))
            target.DataSourceRecordId = source.DataSourceRecordId;
    }
}
