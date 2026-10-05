using System.Text.Json;
using Microsoft.Extensions.Logging;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.ApiSports;

public abstract class ApiSportsServiceBase : BaseApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected ApiSportsServiceBase(
        HttpClient httpClient,
        ILogger logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
    }

    protected async Task<(List<T>? Items, string? Error)> FetchEnvelopeAsync<T>(string url)
        where T : class
    {
        await _rateLimiter.WaitAsync();

        var normalized = url.StartsWith('/') ? url[1..] : url;
        var fullUrl = _httpClient.BaseAddress != null
            ? new Uri(_httpClient.BaseAddress, normalized).ToString()
            : url;

        try
        {
            _logger.LogInformation("Fetching api-sports JSON: {FullUrl}", fullUrl);
            var response = await _httpClient.GetAsync(normalized);
            var json = await response.Content.ReadAsStringAsync();

            var envelope = JsonSerializer.Deserialize<ApiSportsEnvelope<T>>(json, JsonOptions);
            if (envelope == null)
                return (null, "request failed");

            if (envelope.HasErrors)
            {
                var errorsJson = envelope.Errors.GetRawText();
                _logger.LogError("api-sports API returned errors: {Errors}", errorsJson);
                return (null, errorsJson);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "api-sports returned HTTP {StatusCode} without envelope errors for {FullUrl}",
                    (int)response.StatusCode, fullUrl);
                return (null, $"HTTP {(int)response.StatusCode}");
            }

            return (envelope.Response, null);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization failed for {FullUrl}", fullUrl);
            return (null, "request failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch {FullUrl}", fullUrl);
            return (null, "request failed");
        }
    }
}
