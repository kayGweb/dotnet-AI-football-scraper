using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public interface IPlayerTeamSeasonRepository : IRepository<PlayerTeamSeason>
{
    Task UpsertAsync(int playerId, int teamSeasonId, int season);
}
