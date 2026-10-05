using Microsoft.Extensions.Logging;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.ApiSports;

public class ApiSportsStatsScraperStub : ApiSportsServiceBase, IStatsScraperService
{
    private const string Message = "ApiSports stats scrape lands in S1c";

    public ApiSportsStatsScraperStub(
        HttpClient httpClient,
        ILogger<ApiSportsStatsScraperStub> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
    }

    public Task<ScrapeResult> ScrapePlayerStatsAsync(int season, int week, NflSeasonType seasonType = NflSeasonType.Regular) =>
        Task.FromResult(ScrapeResult.Failed(Message));
}
