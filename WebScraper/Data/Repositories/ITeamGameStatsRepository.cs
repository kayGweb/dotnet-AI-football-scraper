using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public interface ITeamGameStatsRepository : IRepository<TeamGameStats>
{
    Task<IEnumerable<TeamGameStats>> GetByGameAsync(int gameId);
    Task<TeamGameStats?> GetByGameAndTeamAsync(int gameId, int teamId);
    Task UpsertAsync(TeamGameStats teamGameStats);
}
