using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Services;
using WebScraper.Services.Scrapers.NflCom;

namespace WebScraper.Tests.Scrapers.NflCom;

public class NflComTeamServiceTests
{
    private static readonly string SampleTeamsJson = """
    {
        "teams": [
            {
                "abbreviation": "KC",
                "fullName": "Kansas City Chiefs",
                "nickName": "Chiefs",
                "cityStateRegion": "Kansas City",
                "conference": "AFC",
                "division": "West"
            },
            {
                "abbreviation": "BUF",
                "fullName": "Buffalo Bills",
                "nickName": "Bills",
                "cityStateRegion": "Buffalo",
                "conference": "AFC",
                "division": "East"
            }
        ]
    }
    """;

    private static RateLimiterService CreateRateLimiter()
    {
        return new RateLimiterService(Options.Create(new ScraperSettings { RequestDelayMs = 0 }));
    }

    private static NflComTeamService CreateService(
        HttpMessageHandler handler,
        ITeamRepository? teamRepo = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://nfl.test") };
        var logger = NullLogger<NflComTeamService>.Instance;
        var providerSettings = new ApiProviderSettings { AuthType = "None" };
        var repo = teamRepo ?? new Mock<ITeamRepository>().Object;
        return new NflComTeamService(httpClient, logger, providerSettings, CreateRateLimiter(), repo);
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
    public async Task ScrapeTeamAsync_CaseInsensitive_ShouldMatchTeam()
    {
        var handler = new FakeHttpHandler(SampleTeamsJson);
        var mockRepo = new Mock<ITeamRepository>();
        var service = CreateService(handler, mockRepo.Object);

        var result = await service.ScrapeTeamAsync("kc");

        Assert.True(result.Success);
        Assert.Equal(1, result.RecordsProcessed);
        mockRepo.Verify(r => r.UpsertAsync(It.IsAny<Team>()), Times.Once);
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
        {
            "teams": [
                {
                    "abbreviation": "KC",
                    "fullName": "",
                    "nickName": "Chiefs",
                    "cityStateRegion": "Kansas City",
                    "conference": "AFC",
                    "division": "West"
                }
            ]
        }
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

    [Fact]
    public async Task ScrapeTeamsAsync_UnexpectedJsonStructure_ShouldHandleGracefully()
    {
        var json = """{"unexpectedField": "value"}""";
        var handler = new FakeHttpHandler(json);
        var mockRepo = new Mock<ITeamRepository>();
        var service = CreateService(handler, mockRepo.Object);

        var result = await service.ScrapeTeamsAsync();

        Assert.NotNull(result);
        // Empty teams list should be deserialized (default), no upserts
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
