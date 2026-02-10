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

public class EspnGameServiceTests
{
    private static readonly string SampleScoreboardJson = """
    {
        "events": [
            {
                "id": "401547417",
                "date": "2025-09-07T17:00Z",
                "season": { "year": 2025, "type": 2 },
                "week": { "number": 1 },
                "competitions": [
                    {
                        "competitors": [
                            {
                                "homeAway": "home",
                                "team": { "id": "12", "abbreviation": "KC" },
                                "score": "27"
                            },
                            {
                                "homeAway": "away",
                                "team": { "id": "2", "abbreviation": "BUF" },
                                "score": "24"
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

    private static (EspnGameService Service, Mock<IGameRepository> GameRepo, Mock<ITeamRepository> TeamRepo)
        CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://espn.test") };
        var logger = NullLogger<EspnGameService>.Instance;
        var providerSettings = new ApiProviderSettings { AuthType = "None" };
        var gameRepo = new Mock<IGameRepository>();
        var teamRepo = new Mock<ITeamRepository>();
        var service = new EspnGameService(httpClient, logger, providerSettings, CreateRateLimiter(),
            gameRepo.Object, teamRepo.Object);
        return (service, gameRepo, teamRepo);
    }

    private static void SetupTeamLookup(Mock<ITeamRepository> teamRepo)
    {
        teamRepo.Setup(r => r.GetByAbbreviationAsync("KC"))
            .ReturnsAsync(new Team { Id = 1, Abbreviation = "KC", Name = "Kansas City Chiefs" });
        teamRepo.Setup(r => r.GetByAbbreviationAsync("BUF"))
            .ReturnsAsync(new Team { Id = 2, Abbreviation = "BUF", Name = "Buffalo Bills" });
    }

    [Fact]
    public async Task ScrapeGamesAsync_WithWeek_ShouldParseAndUpsertGame()
    {
        var handler = new FakeHttpHandler(SampleScoreboardJson);
        var (service, gameRepo, teamRepo) = CreateService(handler);
        SetupTeamLookup(teamRepo);

        await service.ScrapeGamesAsync(2025, 1);

        gameRepo.Verify(r => r.UpsertAsync(It.IsAny<Game>()), Times.Once);
    }

    [Fact]
    public async Task ScrapeGamesAsync_ShouldMapHomeAndAwayTeamsCorrectly()
    {
        var handler = new FakeHttpHandler(SampleScoreboardJson);
        var (service, gameRepo, teamRepo) = CreateService(handler);
        SetupTeamLookup(teamRepo);

        Game? capturedGame = null;
        gameRepo.Setup(r => r.UpsertAsync(It.IsAny<Game>()))
            .Callback<Game>(g => capturedGame = g)
            .Returns(Task.CompletedTask);

        await service.ScrapeGamesAsync(2025, 1);

        Assert.NotNull(capturedGame);
        Assert.Equal(1, capturedGame.HomeTeamId);  // KC = ID 1
        Assert.Equal(2, capturedGame.AwayTeamId);   // BUF = ID 2
    }

    [Fact]
    public async Task ScrapeGamesAsync_ShouldParseScoresCorrectly()
    {
        var handler = new FakeHttpHandler(SampleScoreboardJson);
        var (service, gameRepo, teamRepo) = CreateService(handler);
        SetupTeamLookup(teamRepo);

        Game? capturedGame = null;
        gameRepo.Setup(r => r.UpsertAsync(It.IsAny<Game>()))
            .Callback<Game>(g => capturedGame = g)
            .Returns(Task.CompletedTask);

        await service.ScrapeGamesAsync(2025, 1);

        Assert.NotNull(capturedGame);
        Assert.Equal(27, capturedGame.HomeScore);
        Assert.Equal(24, capturedGame.AwayScore);
    }

    [Fact]
    public async Task ScrapeGamesAsync_ShouldSetSeasonAndWeek()
    {
        var handler = new FakeHttpHandler(SampleScoreboardJson);
        var (service, gameRepo, teamRepo) = CreateService(handler);
        SetupTeamLookup(teamRepo);

        Game? capturedGame = null;
        gameRepo.Setup(r => r.UpsertAsync(It.IsAny<Game>()))
            .Callback<Game>(g => capturedGame = g)
            .Returns(Task.CompletedTask);

        await service.ScrapeGamesAsync(2025, 1);

        Assert.NotNull(capturedGame);
        Assert.Equal(2025, capturedGame.Season);
        Assert.Equal(1, capturedGame.Week);
    }

    [Fact]
    public async Task ScrapeGamesAsync_TeamNotInDb_ShouldSkipGame()
    {
        var handler = new FakeHttpHandler(SampleScoreboardJson);
        var (service, gameRepo, teamRepo) = CreateService(handler);
        // Don't set up team lookups -> both return null

        await service.ScrapeGamesAsync(2025, 1);

        gameRepo.Verify(r => r.UpsertAsync(It.IsAny<Game>()), Times.Never);
    }

    [Fact]
    public async Task ScrapeGamesAsync_NullResponse_ShouldNotThrow()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError);
        var (service, gameRepo, _) = CreateService(handler);

        await service.ScrapeGamesAsync(2025, 1);

        gameRepo.Verify(r => r.UpsertAsync(It.IsAny<Game>()), Times.Never);
    }

    [Fact]
    public async Task ScrapeGamesAsync_NoCompetitions_ShouldSkip()
    {
        var json = """
        {
            "events": [
                {
                    "id": "123",
                    "date": "2025-09-07T17:00Z",
                    "competitions": []
                }
            ]
        }
        """;
        var handler = new FakeHttpHandler(json);
        var (service, gameRepo, _) = CreateService(handler);

        await service.ScrapeGamesAsync(2025, 1);

        gameRepo.Verify(r => r.UpsertAsync(It.IsAny<Game>()), Times.Never);
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
