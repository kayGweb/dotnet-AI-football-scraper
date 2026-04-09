using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Services;
using WebScraper.Services.Scrapers.SportsDataIo;

namespace WebScraper.Tests.Scrapers.SportsDataIo;

public class SportsDataTeamServiceTests
{
    private static readonly string SampleTeamsJson = """
    [
        {
            "TeamID": 1,
            "Key": "KC",
            "FullName": "Kansas City Chiefs",
            "City": "Kansas City",
            "Conference": "AFC",
            "Division": "West"
        },
        {
            "TeamID": 2,
            "Key": "BUF",
            "FullName": "Buffalo Bills",
            "City": "Buffalo",
            "Conference": "AFC",
            "Division": "East"
        }
    ]
    """;

    private static RateLimiterService CreateRateLimiter()
    {
        return new RateLimiterService(Options.Create(new ScraperSettings { RequestDelayMs = 0 }));
    }

    private static SportsDataTeamService CreateService(
        HttpMessageHandler handler,
        ITeamRepository? teamRepo = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://sportsdata.test") };
        var logger = NullLogger<SportsDataTeamService>.Instance;
        var providerSettings = new ApiProviderSettings
        {
            AuthType = "Header",
            ApiKey = "test-key",
            AuthHeaderName = "Ocp-Apim-Subscription-Key"
        };
        var repo = teamRepo ?? new Mock<ITeamRepository>().Object;
        return new SportsDataTeamService(httpClient, logger, providerSettings, CreateRateLimiter(), repo);
    }

    [Fact]
    public async Task ScrapeTeamsAsync_ShouldParseAndUpsertTeams()
    {
        var handler = new FakeHttpHandler(SampleTeamsJson);
        var mockRepo = new Mock<ITeamRepository>();
        var service = CreateService(handler, mockRepo.Object);

        var result = await service.ScrapeTeamsAsync();

        Assert.True(result.Success);
        Assert.Equal(2, result.RecordsProcessed);
        mockRepo.Verify(r => r.UpsertAsync(It.IsAny<Team>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ScrapeTeamsAsync_ShouldMapFieldsCorrectly()
    {
        var handler = new FakeHttpHandler(SampleTeamsJson);
        var capturedTeams = new List<Team>();
        var mockRepo = new Mock<ITeamRepository>();
        mockRepo.Setup(r => r.UpsertAsync(It.IsAny<Team>()))
            .Callback<Team>(t => capturedTeams.Add(t))
            .Returns(Task.CompletedTask);

        var service = CreateService(handler, mockRepo.Object);
        var result = await service.ScrapeTeamsAsync();

        Assert.True(result.Success);
        var kc = capturedTeams.First(t => t.Abbreviation == "KC");
        Assert.Equal("Kansas City Chiefs", kc.Name);
        Assert.Equal("Kansas City", kc.City);
        Assert.Equal("AFC", kc.Conference);
        Assert.Equal("West", kc.Division);
    }

    [Fact]
    public async Task ScrapeTeamAsync_ShouldOnlyUpsertMatchingTeam()
    {
        var handler = new FakeHttpHandler(SampleTeamsJson);
        var capturedTeams = new List<Team>();
        var mockRepo = new Mock<ITeamRepository>();
        mockRepo.Setup(r => r.UpsertAsync(It.IsAny<Team>()))
            .Callback<Team>(t => capturedTeams.Add(t))
            .Returns(Task.CompletedTask);

        var service = CreateService(handler, mockRepo.Object);
        var result = await service.ScrapeTeamAsync("KC");

        Assert.True(result.Success);
        Assert.Equal(1, result.RecordsProcessed);
        Assert.Single(capturedTeams);
        Assert.Equal("KC", capturedTeams[0].Abbreviation);
    }

    [Fact]
    public async Task ScrapeTeamAsync_NotFound_ShouldNotUpsert()
    {
        var handler = new FakeHttpHandler(SampleTeamsJson);
        var mockRepo = new Mock<ITeamRepository>();
        var service = CreateService(handler, mockRepo.Object);

        var result = await service.ScrapeTeamAsync("FAKE");

        Assert.False(result.Success);
        mockRepo.Verify(r => r.UpsertAsync(It.IsAny<Team>()), Times.Never);
    }

    [Fact]
    public async Task ScrapeTeamsAsync_EmptyFullName_ShouldSkipTeam()
    {
        var json = """
        [
            {
                "TeamID": 1,
                "Key": "KC",
                "FullName": "",
                "City": "Kansas City",
                "Conference": "AFC",
                "Division": "West"
            }
        ]
        """;
        var handler = new FakeHttpHandler(json);
        var mockRepo = new Mock<ITeamRepository>();
        var service = CreateService(handler, mockRepo.Object);

        var result = await service.ScrapeTeamsAsync();

        Assert.Equal(0, result.RecordsProcessed);
        mockRepo.Verify(r => r.UpsertAsync(It.IsAny<Team>()), Times.Never);
    }

    [Fact]
    public async Task ScrapeTeamsAsync_NullResponse_ShouldNotThrow()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError);
        var mockRepo = new Mock<ITeamRepository>();
        var service = CreateService(handler, mockRepo.Object);

        var result = await service.ScrapeTeamsAsync();

        Assert.False(result.Success);
        mockRepo.Verify(r => r.UpsertAsync(It.IsAny<Team>()), Times.Never);
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
