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

    public async Task<Player?> GetByEspnIdAsync(string espnId)
        => await _context.Players.Include(p => p.Team)
            .FirstOrDefaultAsync(p => p.EspnId == espnId);

    public async Task UpsertAsync(Player player)
    {
        if (!string.IsNullOrEmpty(player.EspnId))
        {
            await UpsertByEspnIdAsync(player);
            return;
        }

        var existing = await _context.Players
            .FirstOrDefaultAsync(p => p.Name == player.Name && p.TeamId == player.TeamId);

        if (existing != null)
        {
            ApplyPlayerFields(existing, player);
            _context.Players.Update(existing);
        }
        else
        {
            await _context.Players.AddAsync(player);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<Player> UpsertByEspnIdAsync(Player player)
    {
        if (string.IsNullOrEmpty(player.EspnId))
            throw new ArgumentException("EspnId is required for UpsertByEspnIdAsync.", nameof(player));

        var existing = await _context.Players
            .FirstOrDefaultAsync(p => p.EspnId == player.EspnId);

        if (existing != null)
        {
            ApplyPlayerFields(existing, player);
            if (player.TeamId.HasValue)
                existing.TeamId = player.TeamId;
            _context.Players.Update(existing);
            await _context.SaveChangesAsync();
            return existing;
        }

        await _context.Players.AddAsync(player);
        await _context.SaveChangesAsync();
        return player;
    }

    private static void ApplyPlayerFields(Player target, Player source)
    {
        target.Name = source.Name;
        target.Position = source.Position;
        target.JerseyNumber = source.JerseyNumber;
        target.Height = source.Height;
        target.Weight = source.Weight;
        target.College = source.College;
        if (!string.IsNullOrEmpty(source.EspnId))
            target.EspnId = source.EspnId;
    }
}
