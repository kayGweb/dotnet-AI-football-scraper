using Microsoft.EntityFrameworkCore;
using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public class TeamGameStatsRepository : ITeamGameStatsRepository
{
    private readonly AppDbContext _context;

    public TeamGameStatsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TeamGameStats?> GetByIdAsync(int id)
        => await _context.TeamGameStats
            .Include(tgs => tgs.Game)
            .Include(tgs => tgs.Team)
            .FirstOrDefaultAsync(tgs => tgs.Id == id);

    public async Task<IEnumerable<TeamGameStats>> GetAllAsync()
        => await _context.TeamGameStats
            .Include(tgs => tgs.Game)
            .Include(tgs => tgs.Team)
            .ToListAsync();

    public async Task<TeamGameStats> AddAsync(TeamGameStats entity)
    {
        await _context.TeamGameStats.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(TeamGameStats entity)
    {
        _context.TeamGameStats.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var stats = await _context.TeamGameStats.FindAsync(id);
        if (stats != null)
        {
            _context.TeamGameStats.Remove(stats);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _context.TeamGameStats.AnyAsync(tgs => tgs.Id == id);

    public async Task<IEnumerable<TeamGameStats>> GetByGameAsync(int gameId)
        => await _context.TeamGameStats
            .Include(tgs => tgs.Team)
            .Where(tgs => tgs.GameId == gameId)
            .ToListAsync();

    public async Task<TeamGameStats?> GetByGameAndTeamAsync(int gameId, int teamId)
        => await _context.TeamGameStats
            .FirstOrDefaultAsync(tgs => tgs.GameId == gameId && tgs.TeamId == teamId);

    public async Task UpsertAsync(TeamGameStats teamGameStats)
    {
        var existing = await _context.TeamGameStats
            .FirstOrDefaultAsync(tgs => tgs.GameId == teamGameStats.GameId && tgs.TeamId == teamGameStats.TeamId);

        if (existing != null)
        {
            existing.FirstDowns = teamGameStats.FirstDowns;
            existing.FirstDownsPassing = teamGameStats.FirstDownsPassing;
            existing.FirstDownsRushing = teamGameStats.FirstDownsRushing;
            existing.FirstDownsPenalty = teamGameStats.FirstDownsPenalty;
            existing.ThirdDownMade = teamGameStats.ThirdDownMade;
            existing.ThirdDownAttempts = teamGameStats.ThirdDownAttempts;
            existing.FourthDownMade = teamGameStats.FourthDownMade;
            existing.FourthDownAttempts = teamGameStats.FourthDownAttempts;
            existing.TotalPlays = teamGameStats.TotalPlays;
            existing.TotalYards = teamGameStats.TotalYards;
            existing.NetPassingYards = teamGameStats.NetPassingYards;
            existing.PassCompletions = teamGameStats.PassCompletions;
            existing.PassAttempts = teamGameStats.PassAttempts;
            existing.YardsPerPass = teamGameStats.YardsPerPass;
            existing.InterceptionsThrown = teamGameStats.InterceptionsThrown;
            existing.SacksAgainst = teamGameStats.SacksAgainst;
            existing.SackYardsLost = teamGameStats.SackYardsLost;
            existing.RushingYards = teamGameStats.RushingYards;
            existing.RushingAttempts = teamGameStats.RushingAttempts;
            existing.YardsPerRush = teamGameStats.YardsPerRush;
            existing.RedZoneMade = teamGameStats.RedZoneMade;
            existing.RedZoneAttempts = teamGameStats.RedZoneAttempts;
            existing.Turnovers = teamGameStats.Turnovers;
            existing.FumblesLost = teamGameStats.FumblesLost;
            existing.Penalties = teamGameStats.Penalties;
            existing.PenaltyYards = teamGameStats.PenaltyYards;
            existing.DefensiveTouchdowns = teamGameStats.DefensiveTouchdowns;
            existing.PossessionTime = teamGameStats.PossessionTime;
            _context.TeamGameStats.Update(existing);
        }
        else
        {
            await _context.TeamGameStats.AddAsync(teamGameStats);
        }
        await _context.SaveChangesAsync();
    }
}
