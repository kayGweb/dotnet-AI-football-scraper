using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public interface IGameRepository : IRepository<Game>
{
    Task<IEnumerable<Game>> GetBySeasonAsync(int season, NflSeasonType? seasonType = null);
    Task<IEnumerable<Game>> GetByWeekAsync(int season, int week, NflSeasonType seasonType = NflSeasonType.Regular);
    Task UpsertAsync(Game game);
}
