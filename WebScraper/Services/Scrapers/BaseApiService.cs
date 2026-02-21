using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers;

public abstract class BaseApiService
{
    protected readonly HttpClient _httpClient;
    protected readonly ILogger _logger;
    protected readonly ApiProviderSettings _providerSettings;
    private readonly RateLimiterService _rateLimiter;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected BaseApiService(
        HttpClient httpClient,
        ILogger logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter)
    {
        _httpClient = httpClient;
        _logger = logger;
        _providerSettings = providerSettings;
        _rateLimiter = rateLimiter;

        ConfigureAuth();
    }

    private void ConfigureAuth()
    {
        switch (_providerSettings.AuthType.ToLowerInvariant())
        {
            case "header" when !string.IsNullOrEmpty(_providerSettings.ApiKey)
                            && !string.IsNullOrEmpty(_providerSettings.AuthHeaderName):
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                    _providerSettings.AuthHeaderName, _providerSettings.ApiKey);
                break;

            case "basic" when !string.IsNullOrEmpty(_providerSettings.ApiKey):
                var credentials = Convert.ToBase64String(
                    Encoding.ASCII.GetBytes($"{_providerSettings.ApiKey}:MYSPORTSFEEDS"));
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", credentials);
                break;

            case "none":
                break;

            default:
                _logger.LogWarning(
                    "Unknown or misconfigured AuthType '{AuthType}' — no authentication applied",
                    _providerSettings.AuthType);
                break;
        }

        foreach (var header in _providerSettings.CustomHeaders)
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    /// <summary>
    /// Strips a leading slash so HttpClient resolves the path relative to
    /// BaseAddress rather than treating it as an absolute path from the host root.
    /// </summary>
    private static string NormalizeRelativeUrl(string url)
    {
        return url.StartsWith('/') ? url[1..] : url;
    }

    /// <summary>
    /// Resolves a (normalized) relative URL against the HttpClient's BaseAddress
    /// to produce the full absolute URL for logging and error messages.
    /// </summary>
    private string ResolveFullUrl(string relativeUrl)
    {
        var normalized = NormalizeRelativeUrl(relativeUrl);
        if (_httpClient.BaseAddress != null)
            return new Uri(_httpClient.BaseAddress, normalized).ToString();
        return relativeUrl;
    }

    /// <summary>
    /// Performs a lightweight GET against <paramref name="probeUrl"/> to verify the
    /// API is reachable. Returns true on a 2xx response, false otherwise.
    /// Does not throw; logs warnings on failure.
    /// </summary>
    public async Task<bool> CheckConnectivityAsync(string probeUrl = "/teams")
    {
        var normalized = NormalizeRelativeUrl(probeUrl);
        var fullUrl = ResolveFullUrl(probeUrl);

        try
        {
            _logger.LogInformation("Checking API connectivity via {FullUrl}", fullUrl);
            var response = await _httpClient.GetAsync(normalized, HttpCompletionOption.ResponseHeadersRead);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("API connectivity check passed ({StatusCode}) for {FullUrl}", (int)response.StatusCode, fullUrl);
                return true;
            }

            _logger.LogWarning(
                "API connectivity check failed: {FullUrl} returned {StatusCode}. " +
                "Scraping may fail — verify the provider's BaseUrl in appsettings.json",
                fullUrl, (int)response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "API connectivity check failed: could not reach {FullUrl}. " +
                "Scraping may fail — verify network and BaseUrl in appsettings.json",
                fullUrl);
            return false;
        }
    }

    protected async Task<T?> FetchJsonAsync<T>(string url) where T : class
    {
        await _rateLimiter.WaitAsync();
        var normalized = NormalizeRelativeUrl(url);
        var fullUrl = ResolveFullUrl(url);

        try
        {
            _logger.LogInformation("Fetching JSON: {FullUrl}", fullUrl);

            var response = await _httpClient.GetAsync(normalized);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<T>(json, JsonOptions);

            if (result == null)
            {
                _logger.LogWarning("Deserialized null from {FullUrl}", fullUrl);
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization failed for {FullUrl}", fullUrl);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching {FullUrl}: {StatusCode}", fullUrl, ex.StatusCode);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timed out fetching {FullUrl}", fullUrl);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch {FullUrl}", fullUrl);
            return null;
        }
    }
}
