using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public interface IVenueRepository : IRepository<Venue>
{
    Task<Venue?> GetByEspnIdAsync(string espnId);
    Task UpsertAsync(Venue venue);
}
