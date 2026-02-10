using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Services;
using WebScraper.Services.Scrapers.MySportsFeeds;

namespace WebScraper.Tests.Scrapers.MySportsFeeds;

public class MySportsFeedsTeamServiceTests
{
    private static readonly string SampleTeamsJson = """
    {
        "teams": [
            {
                "team": {
                    "id": 1,
                    "abbreviation": "KC",
                    "name": "Kansas City Chiefs",
                    "city": "Kansas City",
                    "conference": "AFC",
                    "division": "West"
                }
            },
            {
                "team": {
                    "id": 2,
                    "abbreviation": "BUF",
                    "name": "Buffalo Bills",
                    "city": "Buffalo",
                    "conference": "AFC",
                    "division": "East"
                }
            }
        ]
    }
    """;

    private static RateLimiterService CreateRateLimiter()
    {
        return new RateLimiterService(Options.Create(new ScraperSettings { RequestDelayMs = 0 }));
    }

    private static MySportsFeedsTeamService CreateService(
        HttpMessageHandler handler,
        ITeamRepository? teamRepo = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://msf.test") };
        var logger = NullLogger<MySportsFeedsTeamService>.Instance;
        var providerSettings = new ApiProviderSettings
        {
            AuthType = "Basic",
            ApiKey = "test-key"
        };
        var repo = teamRepo ?? new Mock<ITeamRepository>().Object;
        return new MySportsFeedsTeamService(httpClient, logger, providerSettings, CreateRateLimiter(), repo);
    }

    [Fact]
    public async Task ScrapeTeamsAsync_ShouldParseNestedJsonAndUpsertTeams()
    {
        var handler = new FakeHttpHandler(SampleTeamsJson);
        var mockRepo = new Mock<ITeamRepository>();
        var service = CreateService(handler, mockRepo.Object);

        await service.ScrapeTeamsAsync();

        mockRepo.Verify(r => r.UpsertAsync(It.IsAny<Team>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ScrapeTeamsAsync_ShouldMapNestedFieldsCorrectly()
    {
        var handler = new FakeHttpHandler(SampleTeamsJson);
        var capturedTeams = new List<Team>();
        var mockRepo = new Mock<ITeamRepository>();
        mockRepo.Setup(r => r.UpsertAsync(It.IsAny<Team>()))
            .Callback<Team>(t => capturedTeams.Add(t))
            .Returns(Task.CompletedTask);

        var service = CreateService(handler, mockRepo.Object);
        await service.ScrapeTeamsAsync();

        var kc = capturedTeams.First(t => t.Abbreviation == "KC");
        Assert.Equal("Kansas City Chiefs", kc.Name);
        Assert.Equal("Kansas City", kc.City);
        Assert.Equal("AFC", kc.Conference);
        Assert.Equal("West", kc.Division);
    }

    [Fact]
    public async Task ScrapeTeamAsync_ShouldUpsertOnlyMatchingTeam()
    {
        var handler = new FakeHttpHandler(SampleTeamsJson);
        var capturedTeams = new List<Team>();
        var mockRepo = new Mock<ITeamRepository>();
        mockRepo.Setup(r => r.UpsertAsync(It.IsAny<Team>()))
            .Callback<Team>(t => capturedTeams.Add(t))
            .Returns(Task.CompletedTask);

        var service = CreateService(handler, mockRepo.Object);
        await service.ScrapeTeamAsync("BUF");

        Assert.Single(capturedTeams);
        Assert.Equal("BUF", capturedTeams[0].Abbreviation);
    }

    [Fact]
    public async Task ScrapeTeamAsync_NotFound_ShouldNotUpsert()
    {
        var handler = new FakeHttpHandler(SampleTeamsJson);
        var mockRepo = new Mock<ITeamRepository>();
        var service = CreateService(handler, mockRepo.Object);

        await service.ScrapeTeamAsync("FAKE");

        mockRepo.Verify(r => r.UpsertAsync(It.IsAny<Team>()), Times.Never);
    }

    [Fact]
    public async Task ScrapeTeamsAsync_EmptyName_ShouldSkipTeam()
    {
        var json = """
        {
            "teams": [
                {
                    "team": {
                        "id": 1,
                        "abbreviation": "KC",
                        "name": "",
                        "city": "Kansas City"
                    }
                }
            ]
        }
        """;
        var handler = new FakeHttpHandler(json);
        var mockRepo = new Mock<ITeamRepository>();
        var service = CreateService(handler, mockRepo.Object);

        await service.ScrapeTeamsAsync();

        mockRepo.Verify(r => r.UpsertAsync(It.IsAny<Team>()), Times.Never);
    }

    [Fact]
    public async Task ScrapeTeamsAsync_NullConference_ShouldDefaultToEmptyString()
    {
        var json = """
        {
            "teams": [
                {
                    "team": {
                        "id": 1,
                        "abbreviation": "KC",
                        "name": "Kansas City Chiefs",
                        "city": "Kansas City",
                        "conference": null,
                        "division": null
                    }
                }
            ]
        }
        """;
        var handler = new FakeHttpHandler(json);
        var capturedTeams = new List<Team>();
        var mockRepo = new Mock<ITeamRepository>();
        mockRepo.Setup(r => r.UpsertAsync(It.IsAny<Team>()))
            .Callback<Team>(t => capturedTeams.Add(t))
            .Returns(Task.CompletedTask);

        var service = CreateService(handler, mockRepo.Object);
        await service.ScrapeTeamsAsync();

        Assert.Single(capturedTeams);
        Assert.Equal("", capturedTeams[0].Conference);
        Assert.Equal("", capturedTeams[0].Division);
    }

    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;

        public FakeHttpHandler(string responseBody)
        {
            _responseBody = responseBody;
            _statusCode = HttpStatusCode.OK;
        }

        public FakeHttpHandler(HttpStatusCode statusCode)
        {
            _responseBody = "";
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
