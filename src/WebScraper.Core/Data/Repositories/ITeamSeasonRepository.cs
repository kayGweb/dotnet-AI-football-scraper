using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public interface ITeamSeasonRepository : IRepository<TeamSeason>
{
    Task<TeamSeason?> GetByAbbreviationAndSeasonAsync(string abbreviation, int season);
    Task<TeamSeason?> GetByFranchiseAndSeasonAsync(int franchiseId, int season);
    Task<TeamSeason> UpsertAsync(TeamSeason teamSeason);
    Task<TeamSeason> EnsureFromTeamAsync(Team team, int season);
}
