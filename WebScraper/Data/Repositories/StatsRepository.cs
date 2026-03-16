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
            // Passing
            existing.PassAttempts = stats.PassAttempts;
            existing.PassCompletions = stats.PassCompletions;
            existing.PassYards = stats.PassYards;
            existing.PassTouchdowns = stats.PassTouchdowns;
            existing.Interceptions = stats.Interceptions;
            existing.QBRating = stats.QBRating;
            existing.AdjQBR = stats.AdjQBR;
            existing.Sacks = stats.Sacks;
            existing.SackYardsLost = stats.SackYardsLost;

            // Rushing
            existing.RushAttempts = stats.RushAttempts;
            existing.RushYards = stats.RushYards;
            existing.RushTouchdowns = stats.RushTouchdowns;
            existing.LongRushing = stats.LongRushing;

            // Receiving
            existing.Receptions = stats.Receptions;
            existing.ReceivingYards = stats.ReceivingYards;
            existing.ReceivingTouchdowns = stats.ReceivingTouchdowns;
            existing.ReceivingTargets = stats.ReceivingTargets;
            existing.LongReception = stats.LongReception;
            existing.YardsPerReception = stats.YardsPerReception;

            // Fumbles
            existing.Fumbles = stats.Fumbles;
            existing.FumblesLost = stats.FumblesLost;
            existing.FumblesRecovered = stats.FumblesRecovered;

            // Defensive
            existing.TotalTackles = stats.TotalTackles;
            existing.SoloTackles = stats.SoloTackles;
            existing.DefensiveSacks = stats.DefensiveSacks;
            existing.TacklesForLoss = stats.TacklesForLoss;
            existing.PassesDefended = stats.PassesDefended;
            existing.QBHits = stats.QBHits;
            existing.DefensiveTouchdowns = stats.DefensiveTouchdowns;

            // Interceptions (defensive)
            existing.InterceptionsCaught = stats.InterceptionsCaught;
            existing.InterceptionYards = stats.InterceptionYards;
            existing.InterceptionTouchdowns = stats.InterceptionTouchdowns;

            // Kick returns
            existing.KickReturns = stats.KickReturns;
            existing.KickReturnYards = stats.KickReturnYards;
            existing.LongKickReturn = stats.LongKickReturn;
            existing.KickReturnTouchdowns = stats.KickReturnTouchdowns;

            // Punt returns
            existing.PuntReturns = stats.PuntReturns;
            existing.PuntReturnYards = stats.PuntReturnYards;
            existing.LongPuntReturn = stats.LongPuntReturn;
            existing.PuntReturnTouchdowns = stats.PuntReturnTouchdowns;

            // Kicking
            existing.FieldGoalsMade = stats.FieldGoalsMade;
            existing.FieldGoalAttempts = stats.FieldGoalAttempts;
            existing.LongFieldGoal = stats.LongFieldGoal;
            existing.ExtraPointsMade = stats.ExtraPointsMade;
            existing.ExtraPointAttempts = stats.ExtraPointAttempts;
            existing.TotalKickingPoints = stats.TotalKickingPoints;

            // Punting
            existing.Punts = stats.Punts;
            existing.PuntYards = stats.PuntYards;
            existing.GrossAvgPuntYards = stats.GrossAvgPuntYards;
            existing.PuntTouchbacks = stats.PuntTouchbacks;
            existing.PuntsInside20 = stats.PuntsInside20;
            existing.LongPunt = stats.LongPunt;

            _context.PlayerGameStats.Update(existing);
        }
        else
        {
            await _context.PlayerGameStats.AddAsync(stats);
        }
        await _context.SaveChangesAsync();
    }
}
