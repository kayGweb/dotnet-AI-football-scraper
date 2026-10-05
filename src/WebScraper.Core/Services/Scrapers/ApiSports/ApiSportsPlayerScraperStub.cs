using Microsoft.Extensions.Logging;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.ApiSports;

public class ApiSportsPlayerScraperStub : ApiSportsServiceBase, IPlayerScraperService
{
    private const string Message = "ApiSports player scrape lands in S1c";

    public ApiSportsPlayerScraperStub(
        HttpClient httpClient,
        ILogger<ApiSportsPlayerScraperStub> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
    }

    public Task<ScrapeResult> ScrapePlayersAsync(int teamId) =>
        Task.FromResult(ScrapeResult.Failed(Message));

    public Task<ScrapeResult> ScrapeAllPlayersAsync() =>
        Task.FromResult(ScrapeResult.Failed(Message));
}
