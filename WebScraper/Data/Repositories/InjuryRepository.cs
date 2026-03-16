using Microsoft.EntityFrameworkCore;
using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public class InjuryRepository : IInjuryRepository
{
    private readonly AppDbContext _context;

    public InjuryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Injury?> GetByIdAsync(int id)
        => await _context.Injuries
            .Include(i => i.Game)
            .Include(i => i.Player)
            .FirstOrDefaultAsync(i => i.Id == id);

    public async Task<IEnumerable<Injury>> GetAllAsync()
        => await _context.Injuries
            .Include(i => i.Game)
            .Include(i => i.Player)
            .ToListAsync();

    public async Task<Injury> AddAsync(Injury entity)
    {
        await _context.Injuries.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Injury entity)
    {
        _context.Injuries.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var injury = await _context.Injuries.FindAsync(id);
        if (injury != null)
        {
            _context.Injuries.Remove(injury);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _context.Injuries.AnyAsync(i => i.Id == id);

    public async Task<IEnumerable<Injury>> GetByGameAsync(int gameId)
        => await _context.Injuries
            .Include(i => i.Player)
            .Where(i => i.GameId == gameId)
            .ToListAsync();

    public async Task<Injury?> GetByGameAndAthleteAsync(int gameId, string espnAthleteId)
        => await _context.Injuries
            .FirstOrDefaultAsync(i => i.GameId == gameId && i.EspnAthleteId == espnAthleteId);

    public async Task UpsertAsync(Injury injury)
    {
        var existing = await _context.Injuries
            .FirstOrDefaultAsync(i => i.GameId == injury.GameId && i.EspnAthleteId == injury.EspnAthleteId);

        if (existing != null)
        {
            existing.PlayerId = injury.PlayerId;
            existing.PlayerName = injury.PlayerName;
            existing.Status = injury.Status;
            existing.InjuryType = injury.InjuryType;
            existing.BodyLocation = injury.BodyLocation;
            existing.Side = injury.Side;
            existing.Detail = injury.Detail;
            existing.ReturnDate = injury.ReturnDate;
            existing.ReportDate = injury.ReportDate;
            _context.Injuries.Update(existing);
        }
        else
        {
            await _context.Injuries.AddAsync(injury);
        }
        await _context.SaveChangesAsync();
    }
}
