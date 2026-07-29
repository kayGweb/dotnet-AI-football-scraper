using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public interface IFranchiseRepository : IRepository<Franchise>
{
    Task<Franchise?> GetByCanonicalAbbreviationAsync(string abbreviation);
    Task<Franchise> GetOrCreateAsync(string canonicalAbbreviation, string displayName);
}
