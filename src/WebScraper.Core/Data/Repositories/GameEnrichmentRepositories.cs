using Microsoft.EntityFrameworkCore;
using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public class GameDriveRepository : IGameDriveRepository
{
    private readonly AppDbContext _context;

    public GameDriveRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<GameDrive>> GetByGameAsync(int gameId)
        => await _context.GameDrives
            .Where(d => d.GameId == gameId)
            .OrderBy(d => d.Sequence)
            .ToListAsync();

    public async Task ReplaceForGameAsync(int gameId, IEnumerable<GameDrive> drives)
    {
        var existing = await _context.GameDrives.Where(d => d.GameId == gameId).ToListAsync();
        _context.GameDrives.RemoveRange(existing);
        await _context.GameDrives.AddRangeAsync(drives);
        await _context.SaveChangesAsync();
    }
}

public class ScoringPlayRepository : IScoringPlayRepository
{
    private readonly AppDbContext _context;

    public ScoringPlayRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<ScoringPlay>> GetByGameAsync(int gameId)
        => await _context.ScoringPlays
            .Where(p => p.GameId == gameId)
            .OrderBy(p => p.Sequence)
            .ToListAsync();

    public async Task ReplaceForGameAsync(int gameId, IEnumerable<ScoringPlay> plays)
    {
        var existing = await _context.ScoringPlays.Where(p => p.GameId == gameId).ToListAsync();
        _context.ScoringPlays.RemoveRange(existing);
        await _context.ScoringPlays.AddRangeAsync(plays);
        await _context.SaveChangesAsync();
    }
}

public class GameWeatherRepository : IGameWeatherRepository
{
    private readonly AppDbContext _context;

    public GameWeatherRepository(AppDbContext context) => _context = context;

    public async Task<GameWeather?> GetByGameAsync(int gameId)
        => await _context.GameWeathers.FirstOrDefaultAsync(w => w.GameId == gameId);

    public async Task UpsertAsync(GameWeather weather)
    {
        var existing = await _context.GameWeathers.FirstOrDefaultAsync(w => w.GameId == weather.GameId);
        if (existing is null)
        {
            await _context.GameWeathers.AddAsync(weather);
        }
        else
        {
            existing.TemperatureF = weather.TemperatureF;
            existing.HighTemperatureF = weather.HighTemperatureF;
            existing.Condition = weather.Condition;
            existing.WindSpeedMph = weather.WindSpeedMph;
            existing.WindDirection = weather.WindDirection;
            existing.HumidityPercent = weather.HumidityPercent;
            existing.DataSource = weather.DataSource;
            existing.DataSourceFetchedAt = weather.DataSourceFetchedAt;
        }

        await _context.SaveChangesAsync();
    }
}

public class GameOfficialRepository : IGameOfficialRepository
{
    private readonly AppDbContext _context;

    public GameOfficialRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<GameOfficial>> GetByGameAsync(int gameId)
        => await _context.GameOfficials
            .Where(o => o.GameId == gameId)
            .OrderBy(o => o.SortOrder)
            .ToListAsync();

    public async Task ReplaceForGameAsync(int gameId, IEnumerable<GameOfficial> officials)
    {
        var existing = await _context.GameOfficials.Where(o => o.GameId == gameId).ToListAsync();
        _context.GameOfficials.RemoveRange(existing);
        await _context.GameOfficials.AddRangeAsync(officials);
        await _context.SaveChangesAsync();
    }
}

public class GameOddsRepository : IGameOddsRepository
{
    private readonly AppDbContext _context;

    public GameOddsRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<GameOdds>> GetByGameAsync(int gameId)
        => await _context.GameOdds
            .Where(o => o.GameId == gameId)
            .OrderByDescending(o => o.CapturedAt)
            .ToListAsync();

    public async Task AddSnapshotAsync(GameOdds odds)
    {
        var exists = await _context.GameOdds.AnyAsync(o =>
            o.GameId == odds.GameId &&
            o.Sportsbook == odds.Sportsbook &&
            o.SnapshotType == odds.SnapshotType &&
            o.CapturedAt == odds.CapturedAt);

        if (!exists)
        {
            await _context.GameOdds.AddAsync(odds);
            await _context.SaveChangesAsync();
        }
    }
}
