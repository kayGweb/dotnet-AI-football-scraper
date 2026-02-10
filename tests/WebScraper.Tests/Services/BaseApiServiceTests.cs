using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WebScraper.Models;
using WebScraper.Services;
using WebScraper.Services.Scrapers;

namespace WebScraper.Tests.Services;

/// <summary>
/// Concrete test implementation of BaseApiService to test protected/private behavior.
/// </summary>
public class TestApiService : BaseApiService
{
    public TestApiService(
        HttpClient httpClient,
        ILogger logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter)
        : base(httpClient, logger, providerSettings, rateLimiter) { }

    /// <summary>Expose FetchJsonAsync for testing.</summary>
    public Task<T?> TestFetchJsonAsync<T>(string url) where T : class
        => FetchJsonAsync<T>(url);
}

public class BaseApiServiceTests
{
    private static RateLimiterService CreateRateLimiter()
    {
        return new RateLimiterService(Options.Create(new ScraperSettings { RequestDelayMs = 0 }));
    }

    private static TestApiService CreateService(
        HttpMessageHandler handler,
        ApiProviderSettings? settings = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test.local") };
        var logger = NullLogger<TestApiService>.Instance;
        var providerSettings = settings ?? new ApiProviderSettings { AuthType = "None" };
        return new TestApiService(httpClient, logger, providerSettings, CreateRateLimiter());
    }

    // --- FetchJsonAsync tests ---

    [Fact]
    public async Task FetchJsonAsync_ShouldDeserializeValidJson()
    {
        var handler = new FakeHttpHandler("""{"name":"Test","value":42}""", "application/json");
        var service = CreateService(handler);

        var result = await service.TestFetchJsonAsync<TestDto>("/api/test");

        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task FetchJsonAsync_ShouldReturnNull_OnMalformedJson()
    {
        var handler = new FakeHttpHandler("{ not valid json !!!", "application/json");
        var service = CreateService(handler);

        var result = await service.TestFetchJsonAsync<TestDto>("/api/test");

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchJsonAsync_ShouldReturnNull_OnHttpError()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError);
        var service = CreateService(handler);

        var result = await service.TestFetchJsonAsync<TestDto>("/api/test");

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchJsonAsync_ShouldReturnNull_OnEmptyResponse()
    {
        var handler = new FakeHttpHandler("null", "application/json");
        var service = CreateService(handler);

        var result = await service.TestFetchJsonAsync<TestDto>("/api/test");

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchJsonAsync_ShouldDeserializeCaseInsensitively()
    {
        var handler = new FakeHttpHandler("""{"NAME":"Test","VALUE":42}""", "application/json");
        var service = CreateService(handler);

        var result = await service.TestFetchJsonAsync<TestDto>("/api/test");

        Assert.NotNull(result);
        Assert.Equal("Test", result.Name);
        Assert.Equal(42, result.Value);
    }

    // --- Auth configuration tests ---

    [Fact]
    public void ConfigureAuth_HeaderType_ShouldAddApiKeyHeader()
    {
        var handler = new FakeHttpHandler("", "application/json");
        var settings = new ApiProviderSettings
        {
            AuthType = "Header",
            ApiKey = "test-api-key-123",
            AuthHeaderName = "Ocp-Apim-Subscription-Key"
        };
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test.local") };
        var logger = NullLogger<TestApiService>.Instance;

        _ = new TestApiService(httpClient, logger, settings, CreateRateLimiter());

        Assert.True(httpClient.DefaultRequestHeaders.Contains("Ocp-Apim-Subscription-Key"));
        Assert.Equal("test-api-key-123",
            httpClient.DefaultRequestHeaders.GetValues("Ocp-Apim-Subscription-Key").First());
    }

    [Fact]
    public void ConfigureAuth_BasicType_ShouldAddBasicAuthHeader()
    {
        var handler = new FakeHttpHandler("", "application/json");
        var settings = new ApiProviderSettings
        {
            AuthType = "Basic",
            ApiKey = "my-api-key"
        };
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test.local") };
        var logger = NullLogger<TestApiService>.Instance;

        _ = new TestApiService(httpClient, logger, settings, CreateRateLimiter());

        Assert.NotNull(httpClient.DefaultRequestHeaders.Authorization);
        Assert.Equal("Basic", httpClient.DefaultRequestHeaders.Authorization.Scheme);

        var expectedCredentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes("my-api-key:MYSPORTSFEEDS"));
        Assert.Equal(expectedCredentials, httpClient.DefaultRequestHeaders.Authorization.Parameter);
    }

    [Fact]
    public void ConfigureAuth_NoneType_ShouldNotAddAuthHeaders()
    {
        var handler = new FakeHttpHandler("", "application/json");
        var settings = new ApiProviderSettings { AuthType = "None" };
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test.local") };
        var logger = NullLogger<TestApiService>.Instance;

        _ = new TestApiService(httpClient, logger, settings, CreateRateLimiter());

        Assert.Null(httpClient.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public void ConfigureAuth_HeaderType_WithMissingApiKey_ShouldNotAddHeader()
    {
        var handler = new FakeHttpHandler("", "application/json");
        var settings = new ApiProviderSettings
        {
            AuthType = "Header",
            ApiKey = null,
            AuthHeaderName = "Ocp-Apim-Subscription-Key"
        };
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test.local") };
        var logger = NullLogger<TestApiService>.Instance;

        _ = new TestApiService(httpClient, logger, settings, CreateRateLimiter());

        Assert.False(httpClient.DefaultRequestHeaders.Contains("Ocp-Apim-Subscription-Key"));
    }

    [Fact]
    public void ConfigureAuth_CustomHeaders_ShouldBeApplied()
    {
        var handler = new FakeHttpHandler("", "application/json");
        var settings = new ApiProviderSettings
        {
            AuthType = "None",
            CustomHeaders = new Dictionary<string, string>
            {
                { "X-Custom-Header", "custom-value" },
                { "X-Another", "another-value" }
            }
        };
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test.local") };
        var logger = NullLogger<TestApiService>.Instance;

        _ = new TestApiService(httpClient, logger, settings, CreateRateLimiter());

        Assert.True(httpClient.DefaultRequestHeaders.Contains("X-Custom-Header"));
        Assert.Equal("custom-value",
            httpClient.DefaultRequestHeaders.GetValues("X-Custom-Header").First());
        Assert.True(httpClient.DefaultRequestHeaders.Contains("X-Another"));
    }

    // --- Test helpers ---

    public class TestDto
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly string _contentType;
        private readonly HttpStatusCode _statusCode;

        public FakeHttpHandler(string responseBody, string contentType)
        {
            _responseBody = responseBody;
            _contentType = contentType;
            _statusCode = HttpStatusCode.OK;
        }

        public FakeHttpHandler(HttpStatusCode statusCode)
        {
            _responseBody = "";
            _contentType = "application/json";
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, _contentType)
            });
        }
    }
}
