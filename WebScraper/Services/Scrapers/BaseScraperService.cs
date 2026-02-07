using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers;

public abstract class BaseScraperService
{
    protected readonly HttpClient _httpClient;
    protected readonly ILogger _logger;
    protected readonly ScraperSettings _settings;
    private readonly RateLimiterService _rateLimiter;

    protected BaseScraperService(
        HttpClient httpClient,
        ILogger logger,
        IOptions<ScraperSettings> settings,
        RateLimiterService rateLimiter)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
        _rateLimiter = rateLimiter;

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_settings.UserAgent);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
    }

    protected async Task<HtmlDocument?> FetchPageAsync(string url)
    {
        await _rateLimiter.WaitAsync();

        try
        {
            _logger.LogInformation("Fetching: {Url}", url);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            return doc;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching {Url}: {StatusCode}", url, ex.StatusCode);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timed out fetching {Url}", url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch {Url}", url);
            return null;
        }
    }
}
