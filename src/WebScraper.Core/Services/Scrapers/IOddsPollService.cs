using WebScraper.Models;

namespace WebScraper.Services.Scrapers;

public interface IOddsPollService
{
    /// <summary>
    /// Fetches ESPN pickcenter for eligible games and stores Opening/Current/Closing snapshots.
    /// </summary>
    Task<ScrapeResult> PollAsync(int? season = null, CancellationToken cancellationToken = default);
}
