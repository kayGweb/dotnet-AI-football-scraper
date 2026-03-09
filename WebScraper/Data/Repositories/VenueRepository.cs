using Microsoft.EntityFrameworkCore;
using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public class VenueRepository : IVenueRepository
{
    private readonly AppDbContext _context;

    public VenueRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Venue?> GetByIdAsync(int id)
        => await _context.Venues.FindAsync(id);

    public async Task<IEnumerable<Venue>> GetAllAsync()
        => await _context.Venues.ToListAsync();

    public async Task<Venue> AddAsync(Venue entity)
    {
        await _context.Venues.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Venue entity)
    {
        _context.Venues.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var venue = await _context.Venues.FindAsync(id);
        if (venue != null)
        {
            _context.Venues.Remove(venue);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _context.Venues.AnyAsync(v => v.Id == id);

    public async Task<Venue?> GetByEspnIdAsync(string espnId)
        => await _context.Venues.FirstOrDefaultAsync(v => v.EspnId == espnId);

    public async Task UpsertAsync(Venue venue)
    {
        var existing = await GetByEspnIdAsync(venue.EspnId);
        if (existing != null)
        {
            existing.Name = venue.Name;
            existing.City = venue.City;
            existing.State = venue.State;
            existing.Country = venue.Country;
            existing.IsGrass = venue.IsGrass;
            existing.IsIndoor = venue.IsIndoor;
            _context.Venues.Update(existing);
        }
        else
        {
            await _context.Venues.AddAsync(venue);
        }
        await _context.SaveChangesAsync();
    }
}
