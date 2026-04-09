using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public interface IApiLinkRepository : IRepository<ApiLink>
{
    Task<ApiLink?> GetByUrlAsync(string url);
    Task<IEnumerable<ApiLink>> GetByGameAsync(int gameId);
    Task<IEnumerable<ApiLink>> GetByEndpointTypeAsync(string endpointType);
    Task UpsertAsync(ApiLink apiLink);
}
