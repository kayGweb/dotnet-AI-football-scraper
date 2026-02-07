using Microsoft.EntityFrameworkCore;
using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public class TeamRepository : ITeamRepository
{
    private readonly AppDbContext _context;

    public TeamRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Team?> GetByIdAsync(int id)
        => await _context.Teams.FindAsync(id);

    public async Task<IEnumerable<Team>> GetAllAsync()
        => await _context.Teams.ToListAsync();

    public async Task<Team> AddAsync(Team entity)
    {
        await _context.Teams.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(Team entity)
    {
        _context.Teams.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var team = await _context.Teams.FindAsync(id);
        if (team != null)
        {
            _context.Teams.Remove(team);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _context.Teams.AnyAsync(t => t.Id == id);

    public async Task<Team?> GetByAbbreviationAsync(string abbreviation)
        => await _context.Teams.FirstOrDefaultAsync(t => t.Abbreviation == abbreviation);

    public async Task<IEnumerable<Team>> GetByConferenceAsync(string conference)
        => await _context.Teams.Where(t => t.Conference == conference).ToListAsync();

    public async Task UpsertAsync(Team team)
    {
        var existing = await GetByAbbreviationAsync(team.Abbreviation);
        if (existing != null)
        {
            existing.Name = team.Name;
            existing.City = team.City;
            existing.Conference = team.Conference;
            existing.Division = team.Division;
            _context.Teams.Update(existing);
        }
        else
        {
            await _context.Teams.AddAsync(team);
        }
        await _context.SaveChangesAsync();
    }
}
