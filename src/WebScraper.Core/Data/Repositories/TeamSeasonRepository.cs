using Microsoft.EntityFrameworkCore;
using WebScraper.Models;
using WebScraper.Services;

namespace WebScraper.Data.Repositories;

public class TeamSeasonRepository : ITeamSeasonRepository
{
    private readonly AppDbContext _context;
    private readonly IFranchiseRepository _franchiseRepository;

    public TeamSeasonRepository(AppDbContext context, IFranchiseRepository franchiseRepository)
    {
        _context = context;
        _franchiseRepository = franchiseRepository;
    }

    public async Task<TeamSeason?> GetByIdAsync(int id)
        => await _context.TeamSeasons
            .Include(ts => ts.Franchise)
            .FirstOrDefaultAsync(ts => ts.Id == id);

    public async Task<IEnumerable<TeamSeason>> GetAllAsync()
        => await _context.TeamSeasons.Include(ts => ts.Franchise).ToListAsync();

    public async Task<TeamSeason> AddAsync(TeamSeason entity)
    {
        await _context.TeamSeasons.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(TeamSeason entity)
    {
        _context.TeamSeasons.Update(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var teamSeason = await _context.TeamSeasons.FindAsync(id);
        if (teamSeason != null)
        {
            _context.TeamSeasons.Remove(teamSeason);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(int id)
        => await _context.TeamSeasons.AnyAsync(ts => ts.Id == id);

    public async Task<TeamSeason?> GetByAbbreviationAndSeasonAsync(string abbreviation, int season)
    {
        var canonical = FranchiseMappings.ToCanonicalAbbreviation(abbreviation);
        return await _context.TeamSeasons
            .Include(ts => ts.Franchise)
            .FirstOrDefaultAsync(ts =>
                ts.Season == season &&
                (ts.Abbreviation == abbreviation || ts.Franchise.CanonicalAbbreviation == canonical));
    }

    public async Task<TeamSeason?> GetByFranchiseAndSeasonAsync(int franchiseId, int season)
        => await _context.TeamSeasons
            .FirstOrDefaultAsync(ts => ts.FranchiseId == franchiseId && ts.Season == season);

    public async Task<TeamSeason> UpsertAsync(TeamSeason teamSeason)
    {
        var existing = await _context.TeamSeasons
            .FirstOrDefaultAsync(ts => ts.FranchiseId == teamSeason.FranchiseId && ts.Season == teamSeason.Season);

        if (existing != null)
        {
            existing.Name = teamSeason.Name;
            existing.Abbreviation = teamSeason.Abbreviation;
            existing.City = teamSeason.City;
            existing.Conference = teamSeason.Conference;
            existing.Division = teamSeason.Division;
            _context.TeamSeasons.Update(existing);
            await _context.SaveChangesAsync();
            return existing;
        }

        await _context.TeamSeasons.AddAsync(teamSeason);
        await _context.SaveChangesAsync();
        return teamSeason;
    }

    public async Task<TeamSeason> EnsureFromTeamAsync(Team team, int season)
    {
        var franchise = await _franchiseRepository.GetOrCreateAsync(team.Abbreviation, team.Name);
        var existing = await GetByFranchiseAndSeasonAsync(franchise.Id, season);
        if (existing != null)
            return existing;

        return await UpsertAsync(new TeamSeason
        {
            FranchiseId = franchise.Id,
            Season = season,
            Name = team.Name,
            Abbreviation = team.Abbreviation,
            City = team.City,
            Conference = team.Conference,
            Division = team.Division,
        });
    }
}
