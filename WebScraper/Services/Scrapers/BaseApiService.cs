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

    protected async Task<T?> FetchJsonAsync<T>(string url) where T : class
    {
        await _rateLimiter.WaitAsync();

        try
        {
            _logger.LogInformation("Fetching JSON: {Url}", url);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<T>(json, JsonOptions);

            if (result == null)
            {
                _logger.LogWarning("Deserialized null from {Url}", url);
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization failed for {Url}", url);
            return null;
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
