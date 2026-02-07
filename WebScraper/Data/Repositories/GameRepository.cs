using Microsoft.EntityFrameworkCore;
using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public class GameRepository : IGameRepository
{
    private readonly AppDbContext _context;

    public GameRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Game?> GetByIdAsync(int id)
        => await _context.Games
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .FirstOrDefaultAsync(g => g.Id == id);

    public async Task<IEnumerable<Game>> GetAllAsync()
        => await _context.Games
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .ToListAsync();

    public async Task<Game> AddAsync(Game entity)
    {
        await _context.Games.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Game entity)
    {
        _context.Games.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var game = await _context.Games.FindAsync(id);
        if (game != null)
        {
            _context.Games.Remove(game);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _context.Games.AnyAsync(g => g.Id == id);

    public async Task<IEnumerable<Game>> GetBySeasonAsync(int season)
        => await _context.Games
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Where(g => g.Season == season)
            .OrderBy(g => g.Week)
            .ThenBy(g => g.GameDate)
            .ToListAsync();

    public async Task<IEnumerable<Game>> GetByWeekAsync(int season, int week)
        => await _context.Games
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Where(g => g.Season == season && g.Week == week)
            .OrderBy(g => g.GameDate)
            .ToListAsync();

    public async Task UpsertAsync(Game game)
    {
        var existing = await _context.Games
            .FirstOrDefaultAsync(g =>
                g.Season == game.Season &&
                g.Week == game.Week &&
                g.HomeTeamId == game.HomeTeamId &&
                g.AwayTeamId == game.AwayTeamId);

        if (existing != null)
        {
            existing.GameDate = game.GameDate;
            existing.HomeScore = game.HomeScore;
            existing.AwayScore = game.AwayScore;
            _context.Games.Update(existing);
        }
        else
        {
            await _context.Games.AddAsync(game);
        }
        await _context.SaveChangesAsync();
    }
}
