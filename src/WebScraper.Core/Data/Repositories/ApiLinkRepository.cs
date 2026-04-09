using Microsoft.EntityFrameworkCore;
using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public class ApiLinkRepository : IApiLinkRepository
{
    private readonly AppDbContext _context;

    public ApiLinkRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiLink?> GetByIdAsync(int id)
        => await _context.ApiLinks
            .Include(al => al.Game)
            .Include(al => al.Team)
            .FirstOrDefaultAsync(al => al.Id == id);

    public async Task<IEnumerable<ApiLink>> GetAllAsync()
        => await _context.ApiLinks
            .Include(al => al.Game)
            .Include(al => al.Team)
            .ToListAsync();

    public async Task<ApiLink> AddAsync(ApiLink entity)
    {
        await _context.ApiLinks.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(ApiLink entity)
    {
        _context.ApiLinks.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var apiLink = await _context.ApiLinks.FindAsync(id);
        if (apiLink != null)
        {
            _context.ApiLinks.Remove(apiLink);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _context.ApiLinks.AnyAsync(al => al.Id == id);

    public async Task<ApiLink?> GetByUrlAsync(string url)
        => await _context.ApiLinks.FirstOrDefaultAsync(al => al.Url == url);

    public async Task<IEnumerable<ApiLink>> GetByGameAsync(int gameId)
        => await _context.ApiLinks
            .Where(al => al.GameId == gameId)
            .ToListAsync();

    public async Task<IEnumerable<ApiLink>> GetByEndpointTypeAsync(string endpointType)
        => await _context.ApiLinks
            .Where(al => al.EndpointType == endpointType)
            .ToListAsync();

    public async Task UpsertAsync(ApiLink apiLink)
    {
        var existing = await _context.ApiLinks
            .FirstOrDefaultAsync(al => al.Url == apiLink.Url);

        if (existing != null)
        {
            existing.EndpointType = apiLink.EndpointType;
            existing.RelationType = apiLink.RelationType;
            existing.GameId = apiLink.GameId;
            existing.TeamId = apiLink.TeamId;
            existing.Season = apiLink.Season;
            existing.Week = apiLink.Week;
            existing.EspnEventId = apiLink.EspnEventId;
            existing.LastAccessedAt = apiLink.LastAccessedAt;
            _context.ApiLinks.Update(existing);
        }
        else
        {
            await _context.ApiLinks.AddAsync(apiLink);
        }
        await _context.SaveChangesAsync();
    }
}
