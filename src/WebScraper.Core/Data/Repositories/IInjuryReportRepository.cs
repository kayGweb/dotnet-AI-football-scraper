using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public interface IInjuryReportRepository : IRepository<InjuryReport>
{
    Task<InjuryReport> UpsertAsync(InjuryReport report);
    Task<IReadOnlyList<InjuryReport>> GetCurrentAsync(string? teamAbbreviation = null);
}
