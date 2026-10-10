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

public class ApiSportsStatsServiceTests
{
    private const string GameRecordId = "21572";

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiSports", fileName);

    private static RateLimiterService CreateRateLimiter() =>
        new(Options.Create(new ScraperSettings { RequestDelayMs = 0 }));

    private static readonly Team Raiders = new() { Id = 21, Abbreviation = "LV", Name = "Las Vegas Raiders" };
    private static readonly Team Chiefs = new() { Id = 22, Abbreviation = "KC", Name = "Kansas City Chiefs" };

    private static Game FinalGame() => new()
    {
        Id = 500,
        Season = 2026,
        SeasonType = NflSeasonType.Regular,
        Week = 4,
        HomeTeamSeasonId = 210,
        AwayTeamSeasonId = 220,
        HomeScore = 27,
        AwayScore = 30,
        GameStatus = "Final",
        DataSource = "ApiSports",
        DataSourceRecordId = GameRecordId
    };

    private sealed class Harness
    {
        public required ApiSportsStatsService Service { get; init; }
        public required Mock<IGameRepository> GameRepo { get; init; }
        public required Mock<ITeamRepository> TeamRepo { get; init; }
        public required Mock<IPlayerRepository> PlayerRepo { get; init; }
        public required Mock<IPlayerTeamSeasonRepository> PlayerTeamSeasonRepo { get; init; }
        public required RoutingHttpHandler Handler { get; init; }
        public List<Player> CreatedPlayers { get; } = [];
        public List<PlayerGameStats> UpsertedStats { get; } = [];
        public List<TeamGameStats> UpsertedTeamStats { get; } = [];
    }

    private static async Task<Harness> CreateHarnessAsync(
        string? playerStatsJson = null,
        string? teamStatsJson = null,
        IEnumerable<Game>? games = null)
    {
        playerStatsJson ??= await File.ReadAllTextAsync(FixturePath("game-player-stats.json"));
        teamStatsJson ??= await File.ReadAllTextAsync(FixturePath("game-team-stats.json"));

        var handler = new RoutingHttpHandler(playerStatsJson, teamStatsJson);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://v1.american-football.api-sports.test/") };

        var gameRepo = new Mock<IGameRepository>();
        var teamRepo = new Mock<ITeamRepository>();
        var teamSeasonRepo = new Mock<ITeamSeasonRepository>();
        var playerRepo = new Mock<IPlayerRepository>();
        var playerTeamSeasonRepo = new Mock<IPlayerTeamSeasonRepository>();
        var statsRepo = new Mock<IStatsRepository>();
        var teamGameStatsRepo = new Mock<ITeamGameStatsRepository>();

        var harness = new Harness
        {
            Service = new ApiSportsStatsService(
                httpClient, NullLogger<ApiSportsStatsService>.Instance, new ApiProviderSettings { AuthType = "None" },
                CreateRateLimiter(), gameRepo.Object, teamRepo.Object, teamSeasonRepo.Object, playerRepo.Object,
                playerTeamSeasonRepo.Object, statsRepo.Object, teamGameStatsRepo.Object),
            GameRepo = gameRepo,
            TeamRepo = teamRepo,
            PlayerRepo = playerRepo,
            PlayerTeamSeasonRepo = playerTeamSeasonRepo,
            Handler = handler
        };

        gameRepo.Setup(r => r.GetByWeekAsync(2026, 4, NflSeasonType.Regular))
            .ReturnsAsync(games ?? new[] { FinalGame() });

        teamRepo.Setup(r => r.GetByAbbreviationAsync("LV")).ReturnsAsync(Raiders);
        teamRepo.Setup(r => r.GetByAbbreviationAsync("KC")).ReturnsAsync(Chiefs);
        teamSeasonRepo.Setup(r => r.EnsureFromTeamAsync(It.IsAny<Team>(), 2026))
            .ReturnsAsync((Team t, int season) => new TeamSeason
            {
                Id = t.Abbreviation == "LV" ? 210 : 220,
                Abbreviation = t.Abbreviation,
                Season = season
            });

        var nextId = 1;
        playerRepo.Setup(r => r.GetByExternalIdAsync("ApiSports", It.IsAny<string>()))
            .ReturnsAsync((Player?)null);
        playerRepo.Setup(r => r.UpsertByExternalIdAsync(It.IsAny<Player>()))
            .ReturnsAsync((Player p) =>
            {
                p.Id = nextId++;
                harness.CreatedPlayers.Add(p);
                return p;
            });
        playerTeamSeasonRepo.Setup(r => r.UpsertAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        statsRepo.Setup(r => r.UpsertAsync(It.IsAny<PlayerGameStats>()))
            .Callback<PlayerGameStats>(harness.UpsertedStats.Add)
            .Returns(Task.CompletedTask);
        teamGameStatsRepo.Setup(r => r.UpsertAsync(It.IsAny<TeamGameStats>()))
            .Callback<TeamGameStats>(harness.UpsertedTeamStats.Add)
            .Returns(Task.CompletedTask);

        return harness;
    }

    [Fact]
    public async Task ScrapePlayerStatsAsync_Fixture_UpsertsOneLinePerDistinctPlayer()
    {
        var h = await CreateHarnessAsync();

        var result = await h.Service.ScrapePlayerStatsAsync(2026, 4);

        // Raiders: 1001, 3, 10, 1002, 1003, 1004, 1005, 1006 = 8; Chiefs: 1701..1708 = 8
        Assert.True(result.Success);
        Assert.Equal(16, result.RecordsProcessed);
        Assert.Equal(16, h.UpsertedStats.Count);
        Assert.Empty(result.Errors);
        Assert.All(h.UpsertedStats, s =>
        {
            Assert.Equal(500, s.GameId);
            Assert.Equal("ApiSports", s.DataSource);
            Assert.StartsWith(GameRecordId + ":", s.DataSourceRecordId);
            Assert.True(s.PlayerId > 0);
        });
    }

    [Fact]
    public async Task ScrapePlayerStatsAsync_CallsPlayersThenTeamsEndpointOncePerGame()
    {
        var h = await CreateHarnessAsync();

        await h.Service.ScrapePlayerStatsAsync(2026, 4);

        Assert.Equal(2, h.Handler.Requests.Count);
        Assert.Contains("games/statistics/players?id=21572", h.Handler.Requests[0]);
        Assert.Contains("games/statistics/teams?id=21572", h.Handler.Requests[1]);
    }

    [Fact]
    public async Task ScrapePlayerStatsAsync_AccumulatesGroupsIntoOneLine()
    {
        var h = await CreateHarnessAsync();

        await h.Service.ScrapePlayerStatsAsync(2026, 4);

        var abdullah = h.UpsertedStats.Single(s => s.DataSourceRecordId == $"{GameRecordId}:3");
        Assert.Equal(14, abdullah.RushAttempts);
        Assert.Equal(61, abdullah.RushYards);
        Assert.Equal(4, abdullah.Receptions);
        Assert.Equal(27, abdullah.ReceivingYards);

        var mahomes = h.UpsertedStats.Single(s => s.DataSourceRecordId == $"{GameRecordId}:1701");
        Assert.Equal(29, mahomes.PassCompletions);
        Assert.Equal(41, mahomes.PassAttempts);
        Assert.Equal(312, mahomes.PassYards);
        Assert.Equal(3, mahomes.PassTouchdowns);
    }

    [Fact]
    public async Task ScrapePlayerStatsAsync_DiscoversPlayersFromBoxScoreWithTeamSeasonMembership()
    {
        var h = await CreateHarnessAsync();

        await h.Service.ScrapePlayerStatsAsync(2026, 4);

        Assert.Equal(16, h.CreatedPlayers.Count);
        var kelce = h.CreatedPlayers.Single(p => p.DataSourceRecordId == "1703");
        Assert.Equal("Travis Kelce", kelce.Name);
        Assert.Equal("ApiSports", kelce.DataSource);
        Assert.Equal(Chiefs.Id, kelce.TeamId);

        h.PlayerTeamSeasonRepo.Verify(r => r.UpsertAsync(It.IsAny<int>(), 210, 2026), Times.Exactly(8));
        h.PlayerTeamSeasonRepo.Verify(r => r.UpsertAsync(It.IsAny<int>(), 220, 2026), Times.Exactly(8));
    }

    [Fact]
    public async Task ScrapePlayerStatsAsync_ExistingPlayer_IsReusedNotRecreated()
    {
        var h = await CreateHarnessAsync();
        var existing = new Player { Id = 4242, Name = "Patrick Mahomes", Position = "QB", DataSource = "ApiSports", DataSourceRecordId = "1701" };
        h.PlayerRepo.Setup(r => r.GetByExternalIdAsync("ApiSports", "1701")).ReturnsAsync(existing);

        await h.Service.ScrapePlayerStatsAsync(2026, 4);

        Assert.DoesNotContain(h.CreatedPlayers, p => p.DataSourceRecordId == "1701");
        var mahomes = h.UpsertedStats.Single(s => s.DataSourceRecordId == $"{GameRecordId}:1701");
        Assert.Equal(4242, mahomes.PlayerId);
        h.PlayerTeamSeasonRepo.Verify(r => r.UpsertAsync(4242, 220, 2026), Times.Once);
    }

    [Fact]
    public async Task ScrapePlayerStatsAsync_UpsertsTeamGameStatsForBothTeams()
    {
        var h = await CreateHarnessAsync();

        await h.Service.ScrapePlayerStatsAsync(2026, 4);

        Assert.Equal(2, h.UpsertedTeamStats.Count);
        var raiders = h.UpsertedTeamStats.Single(t => t.TeamSeasonId == 210);
        Assert.Equal(500, raiders.GameId);
        Assert.Equal(21, raiders.FirstDowns);
        Assert.Equal(322, raiders.TotalYards);
        Assert.Equal("29:41", raiders.PossessionTime);
        Assert.Equal($"{GameRecordId}:1", raiders.DataSourceRecordId);

        var chiefs = h.UpsertedTeamStats.Single(t => t.TeamSeasonId == 220);
        Assert.Equal(375, chiefs.TotalYards);
        Assert.Equal(7, chiefs.ThirdDownMade);
        Assert.Equal(12, chiefs.ThirdDownAttempts);
    }

    [Fact]
    public async Task ScrapePlayerStatsAsync_NoGames_ReturnsFailed()
    {
        var h = await CreateHarnessAsync(games: Array.Empty<Game>());

        var result = await h.Service.ScrapePlayerStatsAsync(2026, 4);

        Assert.False(result.Success);
        Assert.Contains("Scrape games first", result.Message);
        Assert.Empty(h.Handler.Requests);
    }

    [Fact]
    public async Task ScrapePlayerStatsAsync_OnlyEspnGames_ReturnsFailedWithHint()
    {
        var espnGame = FinalGame();
        espnGame.DataSource = "Espn";
        espnGame.DataSourceRecordId = "401671716";
        espnGame.EspnEventId = "401671716";
        var h = await CreateHarnessAsync(games: new[] { espnGame });

        var result = await h.Service.ScrapePlayerStatsAsync(2026, 4);

        Assert.False(result.Success);
        Assert.Contains("api-sports game id", result.Message);
        Assert.Empty(h.Handler.Requests);
    }

    [Fact]
    public async Task ScrapePlayerStatsAsync_ScheduledGame_IsSkippedWithoutCalling()
    {
        var scheduled = FinalGame();
        scheduled.GameStatus = "Scheduled";
        scheduled.HomeScore = null;
        scheduled.AwayScore = null;
        var h = await CreateHarnessAsync(games: new[] { scheduled });

        var result = await h.Service.ScrapePlayerStatsAsync(2026, 4);

        Assert.True(result.Success);
        Assert.Equal(0, result.RecordsProcessed);
        Assert.Contains("not started", result.Message);
        Assert.Empty(h.Handler.Requests);
    }

    [Fact]
    public async Task ScrapePlayerStatsAsync_EnvelopeError_ReturnsFailed()
    {
        var errorJson = """{"get":"games/statistics/players","parameters":{},"errors":{"requests":"You have reached the request limit for the day"},"results":0,"response":[]}""";
        var h = await CreateHarnessAsync(playerStatsJson: errorJson, teamStatsJson: errorJson);

        var result = await h.Service.ScrapePlayerStatsAsync(2026, 4);

        Assert.False(result.Success);
        Assert.Equal(0, result.RecordsProcessed);
        Assert.Single(result.Errors);
        Assert.Contains("api-sports error", result.Errors[0]);
        Assert.Single(h.Handler.Requests);
    }

    [Fact]
    public async Task ScrapePlayerStatsAsync_TeamStatsError_KeepsPlayerLinesAndReportsError()
    {
        var errorJson = """{"get":"games/statistics/teams","parameters":{},"errors":{"requests":"limit"},"results":0,"response":[]}""";
        var h = await CreateHarnessAsync(teamStatsJson: errorJson);

        var result = await h.Service.ScrapePlayerStatsAsync(2026, 4);

        Assert.True(result.Success);
        Assert.Equal(16, result.RecordsProcessed);
        Assert.Single(result.Errors);
        Assert.Contains("team statistics", result.Errors[0]);
        Assert.Empty(h.UpsertedTeamStats);
    }

    [Fact]
    public async Task ScrapePlayerStatsAsync_UnknownTeamInBoxScore_SkipsThatTeamOnly()
    {
        var playersJson = await File.ReadAllTextAsync(FixturePath("game-player-stats.json"));
        playersJson = playersJson.Replace("\"id\": 17, \"name\": \"Kansas City Chiefs\"", "\"id\": 888, \"name\": \"Mystery\"");
        var h = await CreateHarnessAsync(playerStatsJson: playersJson);

        var result = await h.Service.ScrapePlayerStatsAsync(2026, 4);

        Assert.True(result.Success);
        Assert.Equal(8, result.RecordsProcessed);
        Assert.All(h.UpsertedStats, s => Assert.Equal(Raiders.Id, h.CreatedPlayers.Single(p => p.Id == s.PlayerId).TeamId));
    }

    [Theory]
    [InlineData("Final", true)]
    [InlineData("In Progress", true)]
    [InlineData(null, true)]
    [InlineData("Scheduled", false)]
    [InlineData("Postponed", false)]
    [InlineData("Cancelled", false)]
    public void HasStarted_FollowsGameStatus(string? status, bool expected)
    {
        var game = FinalGame();
        game.GameStatus = status;

        Assert.Equal(expected, ApiSportsStatsService.HasStarted(game));
    }

    private sealed class RoutingHttpHandler : HttpMessageHandler
    {
        private readonly string _playerStatsJson;
        private readonly string _teamStatsJson;

        public RoutingHttpHandler(string playerStatsJson, string teamStatsJson)
        {
            _playerStatsJson = playerStatsJson;
            _teamStatsJson = teamStatsJson;
        }

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;
            Requests.Add(path);
            var json = path.Contains("statistics/teams", StringComparison.OrdinalIgnoreCase) ? _teamStatsJson : _playerStatsJson;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
