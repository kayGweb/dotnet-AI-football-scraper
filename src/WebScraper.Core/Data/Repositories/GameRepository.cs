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
            .Include(g => g.HomeTeamSeason)
            .Include(g => g.AwayTeamSeason)
            .Include(g => g.Venue)
            .FirstOrDefaultAsync(g => g.Id == id);

    public async Task<IEnumerable<Game>> GetAllAsync()
        => await _context.Games
            .Include(g => g.HomeTeamSeason)
            .Include(g => g.AwayTeamSeason)
            .Include(g => g.Venue)
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

    public async Task<IEnumerable<Game>> GetBySeasonAsync(int season, NflSeasonType? seasonType = null)
    {
        var query = _context.Games
            .Include(g => g.HomeTeamSeason)
            .Include(g => g.AwayTeamSeason)
            .Include(g => g.Venue)
            .Where(g => g.Season == season);

        if (seasonType.HasValue)
            query = query.Where(g => g.SeasonType == seasonType.Value);

        return await query
            .OrderBy(g => g.SeasonType)
            .ThenBy(g => g.Week)
            .ThenBy(g => g.GameDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Game>> GetByWeekAsync(int season, int week, NflSeasonType seasonType = NflSeasonType.Regular)
        => await _context.Games
            .Include(g => g.HomeTeamSeason)
            .Include(g => g.AwayTeamSeason)
            .Include(g => g.Venue)
            .Where(g => g.Season == season && g.Week == week && g.SeasonType == seasonType)
            .OrderBy(g => g.GameDate)
            .ToListAsync();

    public async Task UpsertAsync(Game game)
    {
        var existing = await _context.Games
            .FirstOrDefaultAsync(g =>
                g.Season == game.Season &&
                g.SeasonType == game.SeasonType &&
                g.Week == game.Week &&
                g.HomeTeamSeasonId == game.HomeTeamSeasonId &&
                g.AwayTeamSeasonId == game.AwayTeamSeasonId);

        if (existing != null)
        {
            existing.GameDate = game.GameDate;
            existing.HomeScore = game.HomeScore;
            existing.AwayScore = game.AwayScore;
            existing.VenueId = game.VenueId ?? existing.VenueId;
            existing.Attendance = game.Attendance ?? existing.Attendance;
            existing.NeutralSite = game.NeutralSite;
            existing.EspnEventId = game.EspnEventId ?? existing.EspnEventId;
            existing.GameStatus = game.GameStatus ?? existing.GameStatus;
            existing.HomeWinner = game.HomeWinner ?? existing.HomeWinner;
            existing.HomeQ1 = game.HomeQ1 ?? existing.HomeQ1;
            existing.HomeQ2 = game.HomeQ2 ?? existing.HomeQ2;
            existing.HomeQ3 = game.HomeQ3 ?? existing.HomeQ3;
            existing.HomeQ4 = game.HomeQ4 ?? existing.HomeQ4;
            existing.HomeOT = game.HomeOT ?? existing.HomeOT;
            existing.AwayQ1 = game.AwayQ1 ?? existing.AwayQ1;
            existing.AwayQ2 = game.AwayQ2 ?? existing.AwayQ2;
            existing.AwayQ3 = game.AwayQ3 ?? existing.AwayQ3;
            existing.AwayQ4 = game.AwayQ4 ?? existing.AwayQ4;
            existing.AwayOT = game.AwayOT ?? existing.AwayOT;
            _context.Games.Update(existing);
        }
        else
        {
            await _context.Games.AddAsync(game);
        }

        await _context.SaveChangesAsync();
    }
}
