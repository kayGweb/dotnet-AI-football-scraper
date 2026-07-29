using WebScraper.Models;

namespace WebScraper.Services.Scrapers;

/// <summary>Fallback when the configured data provider does not expose betting odds.</summary>
public class NoOpOddsPollService : IOddsPollService
{
    public Task<ScrapeResult> PollAsync(int? season = null, CancellationToken cancellationToken = default)
        => Task.FromResult(ScrapeResult.Failed("Odds poll is only supported when DataProvider is Espn"));
}
