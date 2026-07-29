using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public interface ITeamGameStatsRepository : IRepository<TeamGameStats>
{
    Task<IEnumerable<TeamGameStats>> GetByGameAsync(int gameId);
    Task<TeamGameStats?> GetByGameAndTeamSeasonAsync(int gameId, int teamSeasonId);
    Task UpsertAsync(TeamGameStats teamGameStats);
}
