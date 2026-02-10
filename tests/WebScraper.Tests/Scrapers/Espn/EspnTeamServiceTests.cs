using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Services;
using WebScraper.Services.Scrapers.Espn;

namespace WebScraper.Tests.Scrapers.Espn;

public class EspnTeamServiceTests
{
    private static readonly string SampleTeamsJson = """
    {
        "sports": [
            {
                "leagues": [
                    {
                        "teams": [
                            {
                                "team": {
                                    "id": "12",
                                    "abbreviation": "KC",
                                    "displayName": "Kansas City Chiefs",
                                    "location": "Kansas City",
                                    "shortDisplayName": "Chiefs"
                                }
                            },
                            {
                                "team": {
                                    "id": "2",
                                    "abbreviation": "BUF",
                                    "displayName": "Buffalo Bills",
                                    "location": "Buffalo",
                                    "shortDisplayName": "Bills"
                                }
                            }
                        ]
                    }
                ]
            }
        ]
    }
    """;

    private static RateLimiterService CreateRateLimiter()
    {
        return new RateLimiterService(Options.Create(new ScraperSettings { RequestDelayMs = 0 }));
    }

    private static EspnTeamService CreateService(
        HttpMessageHandler handler,
        ITeamRepository? teamRepo = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://espn.test") };
        var logger = NullLogger<EspnTeamService>.Instance;
        var providerSettings = new ApiProviderSettings { AuthType = "None" };
        var repo = teamRepo ?? new Mock<ITeamRepository>().Object;
        return new EspnTeamService(httpClient, logger, providerSettings, CreateRateLimiter(), repo);
    }

    [Fact]
    public async Task ScrapeTeamsAsync_ShouldParseEspnJsonAndUpsertTeams()
    {
        var handler = new FakeHttpHandler(SampleTeamsJson);
        var mockRepo = new Mock<ITeamRepository>();
        var service = CreateService(handler, mockRepo.Object);

        await service.ScrapeTeamsAsync();

        // Should upsert 2 teams
        mockRepo.Verify(r => r.UpsertAsync(It.IsAny<Team>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ScrapeTeamsAsync_ShouldMapEspnIdToNflAbbreviation()
    {
        var handler = new FakeHttpHandler(SampleTeamsJson);
        var capturedTeams = new List<Team>();
        var mockRepo = new Mock<ITeamRepository>();
        mockRepo.Setup(r => r.UpsertAsync(It.IsAny<Team>()))
            .Callback<Team>(t => capturedTeams.Add(t))
            .Returns(Task.CompletedTask);

        var service = CreateService(handler, mockRepo.Object);
        await service.ScrapeTeamsAsync();

        // ESPN ID "12" -> "KC", ESPN ID "2" -> "BUF"
        Assert.Contains(capturedTeams, t => t.Abbreviation == "KC" && t.Name == "Kansas City Chiefs");
        Assert.Contains(capturedTeams, t => t.Abbreviation == "BUF" && t.Name == "Buffalo Bills");
    }

    [Fact]
    public async Task ScrapeTeamsAsync_ShouldSetConferenceAndDivision()
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
        Assert.Equal("AFC", kc.Conference);
        Assert.Equal("West", kc.Division);

        var buf = capturedTeams.First(t => t.Abbreviation == "BUF");
        Assert.Equal("AFC", buf.Conference);
        Assert.Equal("East", buf.Division);
    }

    [Fact]
    public async Task ScrapeTeamsAsync_ShouldSetCityFromLocation()
    {
        var handler = new FakeHttpHandler(SampleTeamsJson);
        var capturedTeams = new List<Team>();
        var mockRepo = new Mock<ITeamRepository>();
        mockRepo.Setup(r => r.UpsertAsync(It.IsAny<Team>()))
            .Callback<Team>(t => capturedTeams.Add(t))
            .Returns(Task.CompletedTask);

        var service = CreateService(handler, mockRepo.Object);
        await service.ScrapeTeamsAsync();

        Assert.Contains(capturedTeams, t => t.City == "Kansas City");
        Assert.Contains(capturedTeams, t => t.City == "Buffalo");
    }

    [Fact]
    public async Task ScrapeTeamsAsync_NullResponse_ShouldNotThrow()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError);
        var mockRepo = new Mock<ITeamRepository>();
        var service = CreateService(handler, mockRepo.Object);

        await service.ScrapeTeamsAsync();

        mockRepo.Verify(r => r.UpsertAsync(It.IsAny<Team>()), Times.Never);
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
        await service.ScrapeTeamAsync("KC");

        Assert.Single(capturedTeams);
        Assert.Equal("KC", capturedTeams[0].Abbreviation);
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
    public async Task ScrapeTeamsAsync_EmptyDisplayName_ShouldSkipTeam()
    {
        var json = """
        {
            "sports": [{
                "leagues": [{
                    "teams": [{
                        "team": {
                            "id": "12",
                            "abbreviation": "KC",
                            "displayName": "",
                            "location": "Kansas City",
                            "shortDisplayName": ""
                        }
                    }]
                }]
            }]
        }
        """;
        var handler = new FakeHttpHandler(json);
        var mockRepo = new Mock<ITeamRepository>();
        var service = CreateService(handler, mockRepo.Object);

        await service.ScrapeTeamsAsync();

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
