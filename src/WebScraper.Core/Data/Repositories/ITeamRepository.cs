using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public interface ITeamRepository : IRepository<Team>
{
    Task<Team?> GetByAbbreviationAsync(string abbreviation);
    Task<IEnumerable<Team>> GetByConferenceAsync(string conference);
    Task UpsertAsync(Team team);
}
