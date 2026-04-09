using Microsoft.EntityFrameworkCore;
using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly AppDbContext _context;

    public PlayerRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Player?> GetByIdAsync(int id)
        => await _context.Players.Include(p => p.Team).FirstOrDefaultAsync(p => p.Id == id);

    public async Task<IEnumerable<Player>> GetAllAsync()
        => await _context.Players.Include(p => p.Team).ToListAsync();

    public async Task<Player> AddAsync(Player entity)
    {
        await _context.Players.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Player entity)
    {
        _context.Players.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var player = await _context.Players.FindAsync(id);
        if (player != null)
        {
            _context.Players.Remove(player);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _context.Players.AnyAsync(p => p.Id == id);

    public async Task<IEnumerable<Player>> GetByTeamAsync(int teamId)
        => await _context.Players.Where(p => p.TeamId == teamId).ToListAsync();

    public async Task<Player?> GetByNameAsync(string name)
        => await _context.Players.Include(p => p.Team)
            .FirstOrDefaultAsync(p => p.Name == name);

    public async Task UpsertAsync(Player player)
    {
        var existing = await _context.Players
            .FirstOrDefaultAsync(p => p.Name == player.Name && p.TeamId == player.TeamId);

        if (existing != null)
        {
            existing.Position = player.Position;
            existing.JerseyNumber = player.JerseyNumber;
            existing.Height = player.Height;
            existing.Weight = player.Weight;
            existing.College = player.College;
            _context.Players.Update(existing);
        }
        else
        {
            await _context.Players.AddAsync(player);
        }
        await _context.SaveChangesAsync();
    }
}
