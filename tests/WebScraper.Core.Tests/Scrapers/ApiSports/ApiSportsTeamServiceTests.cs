using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Services;
using WebScraper.Services.Scrapers.ApiSports;

namespace WebScraper.Tests.Scrapers.ApiSports;

public class ApiSportsTeamServiceTests
{
    private static readonly string SampleTeamsEnvelope = """
    {
      "get": "teams",
      "parameters": { "league": "1", "season": "2026" },
      "errors": [],
      "results": 2,
      "response": [
        {
          "team": {
            "id": 17,
            "name": "Kansas City Chiefs",
            "code": "KC",
            "city": "Kansas City",
            "logo": "https://media.api-sports.io/american-football/teams/17.png"
          }
        },
        {
          "team": {
            "id": 999,
            "name": "Unknown Franchise",
            "code": "UNK",
            "city": "Nowhere",
            "logo": null
          }
        }
      ]
    }
    """;

    private static RateLimiterService CreateRateLimiter() =>
        new(Options.Create(new ScraperSettings { RequestDelayMs = 0 }));

    private static ApiSportsTeamService CreateService(HttpMessageHandler handler, ITeamRepository? teamRepo = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://v1.american-football.api-sports.test/") };
        var logger = NullLogger<ApiSportsTeamService>.Instance;
        var providerSettings = new ApiProviderSettings
        {
            AuthType = "Header",
            AuthHeaderName = "x-apisports-key",
            ApiKey = "test-key"
        };
        var repo = teamRepo ?? new Mock<ITeamRepository>().Object;
        return new ApiSportsTeamService(httpClient, logger, providerSettings, CreateRateLimiter(), repo);
    }

    [Fact]
    public async Task ScrapeTeamsAsync_ShouldUpsertKnownTeamsAndSkipUnknownIds()
    {
        var handler = new FakeHttpHandler(SampleTeamsEnvelope);
        var mockRepo = new Mock<ITeamRepository>();
        var service = CreateService(handler, mockRepo.Object);

        var result = await service.ScrapeTeamsAsync();

        Assert.True(result.Success);
        Assert.Equal(1, result.RecordsProcessed);
        mockRepo.Verify(r => r.UpsertAsync(It.Is<Team>(t =>
            t.Abbreviation == "KC" &&
            t.DataSource == "ApiSports" &&
            t.DataSourceRecordId == "17")), Times.Once);
    }

    [Fact]
    public async Task ScrapeTeamsAsync_EnvelopeError_ReturnsFailed()
    {
        var errorJson = """{"get":"teams","parameters":{},"errors":{"token":"Error/Missing application key"},"results":0,"response":[]}""";
        var handler = new FakeHttpHandler(errorJson);
        var service = CreateService(handler);

        var result = await service.ScrapeTeamsAsync();

        Assert.False(result.Success);
        Assert.Contains("api-sports error", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly string _json;

        public FakeHttpHandler(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
        }
    }
}
