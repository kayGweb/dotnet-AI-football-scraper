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

    private static (
        EspnGameService Service,
        Mock<IGameRepository> GameRepo,
        Mock<ITeamRepository> TeamRepo,
        Mock<ITeamSeasonRepository> TeamSeasonRepo,
        Mock<IFranchiseRepository> FranchiseRepo)
        CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://espn.test") };
        var logger = NullLogger<EspnGameService>.Instance;
        var providerSettings = new ApiProviderSettings { AuthType = "None" };
        var gameRepo = new Mock<IGameRepository>();
        var teamRepo = new Mock<ITeamRepository>();
        var teamSeasonRepo = new Mock<ITeamSeasonRepository>();
        var franchiseRepo = new Mock<IFranchiseRepository>();
        var venueRepo = new Mock<IVenueRepository>();
        var apiLinkRepo = new Mock<IApiLinkRepository>();
        var service = new EspnGameService(httpClient, logger, providerSettings, CreateRateLimiter(),
            gameRepo.Object, teamRepo.Object, teamSeasonRepo.Object, franchiseRepo.Object,
            venueRepo.Object, apiLinkRepo.Object);
        return (service, gameRepo, teamRepo, teamSeasonRepo, franchiseRepo);
    }

    private static void SetupTeamLookup(Mock<ITeamRepository> teamRepo, Mock<ITeamSeasonRepository>? teamSeasonRepo = null)
    {
        teamRepo.Setup(r => r.GetByAbbreviationAsync("KC"))
            .ReturnsAsync(new Team { Id = 1, Abbreviation = "KC", Name = "Kansas City Chiefs" });
        teamRepo.Setup(r => r.GetByAbbreviationAsync("BUF"))
            .ReturnsAsync(new Team { Id = 2, Abbreviation = "BUF", Name = "Buffalo Bills" });

        if (teamSeasonRepo != null)
        {
            teamSeasonRepo.Setup(r => r.EnsureFromTeamAsync(It.Is<Team>(t => t.Abbreviation == "KC"), 2025))
                .ReturnsAsync(new TeamSeason { Id = 10, Abbreviation = "KC", Season = 2025 });
            teamSeasonRepo.Setup(r => r.EnsureFromTeamAsync(It.Is<Team>(t => t.Abbreviation == "BUF"), 2025))
                .ReturnsAsync(new TeamSeason { Id = 20, Abbreviation = "BUF", Season = 2025 });
        }
    }

    [Fact]
    public async Task ScrapeGamesAsync_WithWeek_ShouldParseAndUpsertGame()
    {
        var handler = new FakeHttpHandler(SampleScoreboardJson);
        var (service, gameRepo, teamRepo, teamSeasonRepo, _) = CreateService(handler);
        SetupTeamLookup(teamRepo, teamSeasonRepo);

        var result = await service.ScrapeGamesAsync(2025, 1);

        Assert.True(result.Success);
        Assert.Equal(1, result.RecordsProcessed);
        gameRepo.Verify(r => r.UpsertAsync(It.IsAny<Game>()), Times.Once);
    }

    [Fact]
    public async Task ScrapeGamesAsync_ShouldMapHomeAndAwayTeamsCorrectly()
    {
        var handler = new FakeHttpHandler(SampleScoreboardJson);
        var (service, gameRepo, teamRepo, teamSeasonRepo, _) = CreateService(handler);
        SetupTeamLookup(teamRepo, teamSeasonRepo);

        Game? capturedGame = null;
        gameRepo.Setup(r => r.UpsertAsync(It.IsAny<Game>()))
            .Callback<Game>(g => capturedGame = g)
            .Returns(Task.CompletedTask);

        var result = await service.ScrapeGamesAsync(2025, 1);

        Assert.True(result.Success);
        Assert.NotNull(capturedGame);
        Assert.Equal(10, capturedGame.HomeTeamSeasonId);
        Assert.Equal(20, capturedGame.AwayTeamSeasonId);
    }

    [Fact]
    public async Task ScrapeGamesAsync_ShouldParseScoresCorrectly()
    {
        var handler = new FakeHttpHandler(SampleScoreboardJson);
        var (service, gameRepo, teamRepo, teamSeasonRepo, _) = CreateService(handler);
        SetupTeamLookup(teamRepo, teamSeasonRepo);

        Game? capturedGame = null;
        gameRepo.Setup(r => r.UpsertAsync(It.IsAny<Game>()))
            .Callback<Game>(g => capturedGame = g)
            .Returns(Task.CompletedTask);

        var result = await service.ScrapeGamesAsync(2025, 1);

        Assert.True(result.Success);
        Assert.NotNull(capturedGame);
        Assert.Equal(27, capturedGame.HomeScore);
        Assert.Equal(24, capturedGame.AwayScore);
    }

    [Fact]
    public async Task ScrapeGamesAsync_ShouldSetSeasonAndWeek()
    {
        var handler = new FakeHttpHandler(SampleScoreboardJson);
        var (service, gameRepo, teamRepo, teamSeasonRepo, _) = CreateService(handler);
        SetupTeamLookup(teamRepo, teamSeasonRepo);

        Game? capturedGame = null;
        gameRepo.Setup(r => r.UpsertAsync(It.IsAny<Game>()))
            .Callback<Game>(g => capturedGame = g)
            .Returns(Task.CompletedTask);

        var result = await service.ScrapeGamesAsync(2025, 1);

        Assert.True(result.Success);
        Assert.NotNull(capturedGame);
        Assert.Equal(2025, capturedGame.Season);
        Assert.Equal(1, capturedGame.Week);
    }

    [Fact]
    public async Task ScrapeGamesAsync_TeamNotInDb_ShouldCreateTeamSeasonViaFranchise()
    {
        var handler = new FakeHttpHandler(SampleScoreboardJson);
        var (service, gameRepo, _, teamSeasonRepo, franchiseRepo) = CreateService(handler);

        franchiseRepo.Setup(r => r.GetOrCreateAsync("KC", "KC"))
            .ReturnsAsync(new Franchise { Id = 1, CanonicalAbbreviation = "KC", DisplayName = "KC" });
        franchiseRepo.Setup(r => r.GetOrCreateAsync("BUF", "BUF"))
            .ReturnsAsync(new Franchise { Id = 2, CanonicalAbbreviation = "BUF", DisplayName = "BUF" });
        teamSeasonRepo.Setup(r => r.UpsertAsync(It.Is<TeamSeason>(ts => ts.Abbreviation == "KC")))
            .ReturnsAsync(new TeamSeason { Id = 10, Abbreviation = "KC", Season = 2025, FranchiseId = 1 });
        teamSeasonRepo.Setup(r => r.UpsertAsync(It.Is<TeamSeason>(ts => ts.Abbreviation == "BUF")))
            .ReturnsAsync(new TeamSeason { Id = 20, Abbreviation = "BUF", Season = 2025, FranchiseId = 2 });

        var result = await service.ScrapeGamesAsync(2025, 1);

        Assert.True(result.Success);
        Assert.Equal(1, result.RecordsProcessed);
        gameRepo.Verify(r => r.UpsertAsync(It.IsAny<Game>()), Times.Once);
        franchiseRepo.Verify(r => r.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ScrapeGamesAsync_NullResponse_ShouldNotThrow()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError);
        var (service, gameRepo, _, _, _) = CreateService(handler);

        var result = await service.ScrapeGamesAsync(2025, 1);

        Assert.True(result.Success);
        Assert.Equal(0, result.RecordsProcessed);
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
        var (service, gameRepo, _, _, _) = CreateService(handler);

        var result = await service.ScrapeGamesAsync(2025, 1);

        Assert.True(result.Success);
        Assert.Equal(0, result.RecordsProcessed);
        gameRepo.Verify(r => r.UpsertAsync(It.IsAny<Game>()), Times.Never);
    }

    [Fact]
    public async Task ScrapeGamesAsync_ShouldPopulateEventIdCache()
    {
        EspnGameService.ClearEventIdCache();
        var handler = new FakeHttpHandler(SampleScoreboardJson);
        var (service, _, teamRepo, teamSeasonRepo, _) = CreateService(handler);
        SetupTeamLookup(teamRepo, teamSeasonRepo);

        await service.ScrapeGamesAsync(2025, 1);

        Assert.True(EspnGameService.HasEventIdsForWeek(2025, 1));
        Assert.Equal("401547417", EspnGameService.GetEventId(2025, 1, "KC"));
    }

    [Fact]
    public async Task PopulateEventIdsAsync_ShouldHydrateCache()
    {
        EspnGameService.ClearEventIdCache();
        var handler = new FakeHttpHandler(SampleScoreboardJson);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://espn.test/") };
        var rateLimiter = CreateRateLimiter();
        var logger = NullLogger<EspnGameService>.Instance;

        Assert.False(EspnGameService.HasEventIdsForWeek(2025, 1));

        await EspnGameService.PopulateEventIdsAsync(httpClient, logger, rateLimiter, 2025, 1);

        Assert.True(EspnGameService.HasEventIdsForWeek(2025, 1));
        Assert.Equal("401547417", EspnGameService.GetEventId(2025, 1, "KC"));
    }

    [Fact]
    public async Task PopulateEventIdsAsync_AlreadyPopulated_ShouldNotRefetch()
    {
        EspnGameService.ClearEventIdCache();
        var callCount = 0;
        var handler = new CountingHttpHandler(SampleScoreboardJson, () => callCount++);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://espn.test/") };
        var rateLimiter = CreateRateLimiter();
        var logger = NullLogger<EspnGameService>.Instance;

        await EspnGameService.PopulateEventIdsAsync(httpClient, logger, rateLimiter, 2025, 1);
        await EspnGameService.PopulateEventIdsAsync(httpClient, logger, rateLimiter, 2025, 1);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void ClearEventIdCache_ShouldResetState()
    {
        EspnGameService.ClearEventIdCache();
        Assert.False(EspnGameService.HasEventIdsForWeek(2025, 1));
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

    private class CountingHttpHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly Action _onSend;

        public CountingHttpHandler(string responseBody, Action onSend)
        {
            _responseBody = responseBody;
            _onSend = onSend;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _onSend();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
