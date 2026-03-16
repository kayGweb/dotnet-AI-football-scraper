using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public interface IInjuryRepository : IRepository<Injury>
{
    Task<IEnumerable<Injury>> GetByGameAsync(int gameId);
    Task<Injury?> GetByGameAndAthleteAsync(int gameId, string espnAthleteId);
    Task UpsertAsync(Injury injury);
}
