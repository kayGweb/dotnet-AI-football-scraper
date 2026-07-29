using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public interface IPlayerRepository : IRepository<Player>
{
    Task<IEnumerable<Player>> GetByTeamAsync(int teamId);
    Task<Player?> GetByNameAsync(string name);
    Task<Player?> GetByEspnIdAsync(string espnId);
    Task UpsertAsync(Player player);
    Task<Player> UpsertByEspnIdAsync(Player player);
}
