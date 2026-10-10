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

public class ApiSportsPlayerServiceTests
{
    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiSports", fileName);

    private static RateLimiterService CreateRateLimiter() =>
        new(Options.Create(new ScraperSettings { RequestDelayMs = 0 }));

    private static readonly Team Raiders = new() { Id = 21, Abbreviation = "LV", Name = "Las Vegas Raiders" };

    private sealed class Harness
    {
        public required ApiSportsPlayerService Service { get; init; }
        public required Mock<IPlayerRepository> PlayerRepo { get; init; }
        public required Mock<ITeamRepository> TeamRepo { get; init; }
        public required Mock<ITeamSeasonRepository> TeamSeasonRepo { get; init; }
        public required Mock<IPlayerTeamSeasonRepository> PlayerTeamSeasonRepo { get; init; }
        public required RecordingHttpHandler Handler { get; init; }
        public List<Player> Upserted { get; } = [];
    }

    private static Harness CreateHarness(string json)
    {
        var handler = new RecordingHttpHandler(json);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://v1.american-football.api-sports.test/") };
        var providerSettings = new ApiProviderSettings { AuthType = "None" };

        var playerRepo = new Mock<IPlayerRepository>();
        var teamRepo = new Mock<ITeamRepository>();
        var teamSeasonRepo = new Mock<ITeamSeasonRepository>();
        var playerTeamSeasonRepo = new Mock<IPlayerTeamSeasonRepository>();

        var harness = new Harness
        {
            Service = new ApiSportsPlayerService(
                httpClient, NullLogger<ApiSportsPlayerService>.Instance, providerSettings, CreateRateLimiter(),
                playerRepo.Object, teamRepo.Object, teamSeasonRepo.Object, playerTeamSeasonRepo.Object),
            PlayerRepo = playerRepo,
            TeamRepo = teamRepo,
            TeamSeasonRepo = teamSeasonRepo,
            PlayerTeamSeasonRepo = playerTeamSeasonRepo,
            Handler = handler
        };

        teamRepo.Setup(r => r.GetByIdAsync(Raiders.Id)).ReturnsAsync(Raiders);
        teamRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { Raiders });
        teamSeasonRepo.Setup(r => r.EnsureFromTeamAsync(It.IsAny<Team>(), It.IsAny<int>()))
            .ReturnsAsync((Team t, int season) => new TeamSeason { Id = 210, Abbreviation = t.Abbreviation, Season = season });

        var nextId = 1;
        playerRepo.Setup(r => r.UpsertByExternalIdAsync(It.IsAny<Player>()))
            .ReturnsAsync((Player p) =>
            {
                p.Id = nextId++;
                harness.Upserted.Add(p);
                return p;
            });
        playerTeamSeasonRepo.Setup(r => r.UpsertAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        return harness;
    }

    [Fact]
    public async Task ScrapePlayersAsync_Fixture_UpsertsEveryPlayerByExternalId()
    {
        var json = await File.ReadAllTextAsync(FixturePath("players-by-team.json"));
        var h = CreateHarness(json);

        var result = await h.Service.ScrapePlayersAsync(Raiders.Id);

        Assert.True(result.Success);
        Assert.Equal(14, result.RecordsProcessed);
        Assert.Equal(14, h.Upserted.Count);
        Assert.All(h.Upserted, p =>
        {
            Assert.Equal("ApiSports", p.DataSource);
            Assert.False(string.IsNullOrEmpty(p.DataSourceRecordId));
            Assert.Equal(Raiders.Id, p.TeamId);
            Assert.Null(p.EspnId);
            Assert.NotNull(p.DataSourceFetchedAt);
        });
        h.PlayerTeamSeasonRepo.Verify(r => r.UpsertAsync(It.IsAny<int>(), 210, It.IsAny<int>()), Times.Exactly(14));
    }

    [Fact]
    public async Task ScrapePlayersAsync_RequestsTheMappedApiSportsTeamId()
    {
        var json = await File.ReadAllTextAsync(FixturePath("players-by-team.json"));
        var h = CreateHarness(json);

        await h.Service.ScrapePlayersAsync(Raiders.Id);

        var request = Assert.Single(h.Handler.Requests);
        Assert.Contains("players?team=1&season=", request);
    }

    [Fact]
    public async Task ScrapePlayersAsync_ParsesHeightWeightJerseyAndCollege()
    {
        var json = await File.ReadAllTextAsync(FixturePath("players-by-team.json"));
        var h = CreateHarness(json);

        await h.Service.ScrapePlayersAsync(Raiders.Id);

        var adams = h.Upserted.Single(p => p.DataSourceRecordId == "10");
        Assert.Equal("Davante Adams", adams.Name);
        Assert.Equal("WR", adams.Position);
        Assert.Equal(17, adams.JerseyNumber);
        Assert.Equal("6-1", adams.Height);
        Assert.Equal(204, adams.Weight);
        Assert.Equal("Fresno State", adams.College);

        // number: 0 means unknown → null jersey
        Assert.Contains(h.Upserted, p => p.JerseyNumber == null);
    }

    [Fact]
    public void MapToPlayer_RejectsMissingIdOrName()
    {
        var now = DateTime.UtcNow;

        Assert.Null(ApiSportsPlayerService.MapToPlayer(new ApiSportsPlayer { Id = 0, Name = "Nobody" }, 1, now));
        Assert.Null(ApiSportsPlayerService.MapToPlayer(new ApiSportsPlayer { Id = 5, Name = "  " }, 1, now));

        var mapped = ApiSportsPlayerService.MapToPlayer(
            new ApiSportsPlayer { Id = 5, Name = " Some One ", Number = 0, Height = "garbage", Weight = null, College = "" }, 1, now);
        Assert.NotNull(mapped);
        Assert.Equal("Some One", mapped.Name);
        Assert.Null(mapped.JerseyNumber);
        Assert.Null(mapped.Height);
        Assert.Null(mapped.Weight);
        Assert.Null(mapped.College);
        Assert.Equal("5", mapped.DataSourceRecordId);
    }

    [Fact]
    public async Task ScrapePlayersAsync_UnknownTeamId_ReturnsFailed()
    {
        var h = CreateHarness("{}");

        var result = await h.Service.ScrapePlayersAsync(999);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(h.Handler.Requests);
    }

    [Fact]
    public async Task ScrapePlayersAsync_TeamWithoutApiSportsMapping_ReturnsFailedWithoutCalling()
    {
        var h = CreateHarness("{}");
        var oddTeam = new Team { Id = 77, Abbreviation = "XYZ", Name = "Mystery" };
        h.TeamRepo.Setup(r => r.GetByIdAsync(77)).ReturnsAsync(oddTeam);

        var result = await h.Service.ScrapePlayersAsync(77);

        Assert.False(result.Success);
        Assert.Contains("mapping", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(h.Handler.Requests);
    }

    [Fact]
    public async Task ScrapePlayersAsync_EnvelopeError_ReturnsFailed()
    {
        var errorJson = """{"get":"players","parameters":{},"errors":{"token":"Error/Missing application key"},"results":0,"response":[]}""";
        var h = CreateHarness(errorJson);

        var result = await h.Service.ScrapePlayersAsync(Raiders.Id);

        Assert.False(result.Success);
        Assert.Contains("api-sports error", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(h.Upserted);
    }

    [Fact]
    public async Task ScrapeAllPlayersAsync_NoTeams_ReturnsFailed()
    {
        var h = CreateHarness("{}");
        h.TeamRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Array.Empty<Team>());

        var result = await h.Service.ScrapeAllPlayersAsync();

        Assert.False(result.Success);
        Assert.Contains("Scrape teams first", result.Message);
    }

    [Fact]
    public async Task ScrapeAllPlayersAsync_SumsAcrossTeams()
    {
        var json = await File.ReadAllTextAsync(FixturePath("players-by-team.json"));
        var h = CreateHarness(json);
        var chiefs = new Team { Id = 22, Abbreviation = "KC", Name = "Kansas City Chiefs" };
        h.TeamRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { Raiders, chiefs });

        var result = await h.Service.ScrapeAllPlayersAsync();

        Assert.True(result.Success);
        Assert.Equal(28, result.RecordsProcessed);
        Assert.Equal(2, h.Handler.Requests.Count);
        Assert.Contains(h.Handler.Requests, r => r.Contains("team=1&"));
        Assert.Contains(h.Handler.Requests, r => r.Contains("team=17&"));
    }

    private sealed class RecordingHttpHandler : HttpMessageHandler
    {
        private readonly string _json;

        public RecordingHttpHandler(string json) => _json = json;

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri?.PathAndQuery ?? string.Empty);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
        }
    }
}
