using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public interface IStatsRepository : IRepository<PlayerGameStats>
{
    Task<IEnumerable<PlayerGameStats>> GetPlayerStatsAsync(string playerName, int season);
    Task<IEnumerable<PlayerGameStats>> GetGameStatsAsync(int gameId);
    Task UpsertAsync(PlayerGameStats stats);
}
