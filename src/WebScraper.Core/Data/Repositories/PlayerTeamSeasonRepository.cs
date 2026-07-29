using Microsoft.EntityFrameworkCore;
using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public class PlayerTeamSeasonRepository : IPlayerTeamSeasonRepository
{
    private readonly AppDbContext _context;

    public PlayerTeamSeasonRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PlayerTeamSeason?> GetByIdAsync(int id)
        => await _context.PlayerTeamSeasons.FindAsync(id);

    public async Task<IEnumerable<PlayerTeamSeason>> GetAllAsync()
        => await _context.PlayerTeamSeasons.ToListAsync();

    public async Task<PlayerTeamSeason> AddAsync(PlayerTeamSeason entity)
    {
        await _context.PlayerTeamSeasons.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(PlayerTeamSeason entity)
    {
        _context.PlayerTeamSeasons.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var row = await _context.PlayerTeamSeasons.FindAsync(id);
        if (row != null)
        {
            _context.PlayerTeamSeasons.Remove(row);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _context.PlayerTeamSeasons.AnyAsync(pts => pts.Id == id);

    public async Task UpsertAsync(int playerId, int teamSeasonId, int season)
    {
        var existing = await _context.PlayerTeamSeasons
            .FirstOrDefaultAsync(pts => pts.PlayerId == playerId && pts.TeamSeasonId == teamSeasonId);

        if (existing != null)
        {
            existing.Season = season;
            _context.PlayerTeamSeasons.Update(existing);
        }
        else
        {
            await _context.PlayerTeamSeasons.AddAsync(new PlayerTeamSeason
            {
                PlayerId = playerId,
                TeamSeasonId = teamSeasonId,
                Season = season,
            });
        }

        await _context.SaveChangesAsync();
    }
}
