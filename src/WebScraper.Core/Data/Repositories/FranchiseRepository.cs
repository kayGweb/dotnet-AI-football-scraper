using Microsoft.EntityFrameworkCore;
using WebScraper.Models;
using WebScraper.Services;

namespace WebScraper.Data.Repositories;

public class FranchiseRepository : IFranchiseRepository
{
    private readonly AppDbContext _context;

    public FranchiseRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Franchise?> GetByIdAsync(int id)
        => await _context.Franchises.FirstOrDefaultAsync(f => f.Id == id);

    public async Task<IEnumerable<Franchise>> GetAllAsync()
        => await _context.Franchises.ToListAsync();

    public async Task<Franchise> AddAsync(Franchise entity)
    {
        await _context.Franchises.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Franchise entity)
    {
        _context.Franchises.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var franchise = await _context.Franchises.FindAsync(id);
        if (franchise != null)
        {
            _context.Franchises.Remove(franchise);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _context.Franchises.AnyAsync(f => f.Id == id);

    public async Task<Franchise?> GetByCanonicalAbbreviationAsync(string abbreviation)
    {
        var canonical = FranchiseMappings.ToCanonicalAbbreviation(abbreviation);
        return await _context.Franchises
            .FirstOrDefaultAsync(f => f.CanonicalAbbreviation == canonical);
    }

    public async Task<Franchise> GetOrCreateAsync(string canonicalAbbreviation, string displayName)
    {
        var canonical = FranchiseMappings.ToCanonicalAbbreviation(canonicalAbbreviation);
        var existing = await GetByCanonicalAbbreviationAsync(canonical);
        if (existing != null)
            return existing;

        var franchise = new Franchise
        {
            CanonicalAbbreviation = canonical,
            DisplayName = displayName,
        };
        await _context.Franchises.AddAsync(franchise);
        await _context.SaveChangesAsync();
        return franchise;
    }
}
