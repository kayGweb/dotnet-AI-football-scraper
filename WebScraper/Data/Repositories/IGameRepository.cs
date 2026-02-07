using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public interface IGameRepository : IRepository<Game>
{
    Task<IEnumerable<Game>> GetBySeasonAsync(int season);
    Task<IEnumerable<Game>> GetByWeekAsync(int season, int week);
    Task UpsertAsync(Game game);
}
