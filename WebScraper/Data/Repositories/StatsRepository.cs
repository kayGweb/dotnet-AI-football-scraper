using Microsoft.EntityFrameworkCore;
using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public class StatsRepository : IStatsRepository
{
    private readonly AppDbContext _context;

    public StatsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PlayerGameStats?> GetByIdAsync(int id)
        => await _context.PlayerGameStats
            .Include(s => s.Player)
            .Include(s => s.Game)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task<IEnumerable<PlayerGameStats>> GetAllAsync()
        => await _context.PlayerGameStats
            .Include(s => s.Player)
            .Include(s => s.Game)
            .ToListAsync();

    public async Task<PlayerGameStats> AddAsync(PlayerGameStats entity)
    {
        await _context.PlayerGameStats.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(PlayerGameStats entity)
    {
        _context.PlayerGameStats.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var stats = await _context.PlayerGameStats.FindAsync(id);
        if (stats != null)
        {
            _context.PlayerGameStats.Remove(stats);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _context.PlayerGameStats.AnyAsync(s => s.Id == id);

    public async Task<IEnumerable<PlayerGameStats>> GetPlayerStatsAsync(string playerName, int season)
        => await _context.PlayerGameStats
            .Include(s => s.Player)
            .Include(s => s.Game)
            .Where(s => s.Player.Name == playerName && s.Game.Season == season)
            .OrderBy(s => s.Game.Week)
            .ToListAsync();

    public async Task<IEnumerable<PlayerGameStats>> GetGameStatsAsync(int gameId)
        => await _context.PlayerGameStats
            .Include(s => s.Player)
            .Where(s => s.GameId == gameId)
            .ToListAsync();

    public async Task UpsertAsync(PlayerGameStats stats)
    {
        var existing = await _context.PlayerGameStats
            .FirstOrDefaultAsync(s => s.PlayerId == stats.PlayerId && s.GameId == stats.GameId);

        if (existing != null)
        {
            existing.PassAttempts = stats.PassAttempts;
            existing.PassCompletions = stats.PassCompletions;
            existing.PassYards = stats.PassYards;
            existing.PassTouchdowns = stats.PassTouchdowns;
            existing.Interceptions = stats.Interceptions;
            existing.RushAttempts = stats.RushAttempts;
            existing.RushYards = stats.RushYards;
            existing.RushTouchdowns = stats.RushTouchdowns;
            existing.Receptions = stats.Receptions;
            existing.ReceivingYards = stats.ReceivingYards;
            existing.ReceivingTouchdowns = stats.ReceivingTouchdowns;
            _context.PlayerGameStats.Update(existing);
        }
        else
        {
            await _context.PlayerGameStats.AddAsync(stats);
        }
        await _context.SaveChangesAsync();
    }
}
