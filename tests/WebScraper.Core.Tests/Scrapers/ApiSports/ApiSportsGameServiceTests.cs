using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Services;
using WebScraper.Services.Scrapers.ApiSports;

namespace WebScraper.Tests.Scrapers.ApiSports;

public class ApiSportsGameServiceTests
{
    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiSports", fileName);

    private static RateLimiterService CreateRateLimiter() =>
        new(Options.Create(new ScraperSettings { RequestDelayMs = 0 }));

    private static (
        ApiSportsGameService Service,
        Mock<IGameRepository> GameRepo,
        Mock<ITeamRepository> TeamRepo,
        Mock<ITeamSeasonRepository> TeamSeasonRepo)
        CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://v1.american-football.api-sports.test/") };
        var logger = NullLogger<ApiSportsGameService>.Instance;
        var providerSettings = new ApiProviderSettings { AuthType = "None" };
        var gameRepo = new Mock<IGameRepository>();
        var teamRepo = new Mock<ITeamRepository>();
        var teamSeasonRepo = new Mock<ITeamSeasonRepository>();
        var venueRepo = new Mock<IVenueRepository>();
        venueRepo.Setup(r => r.UpsertAsync(It.IsAny<Venue>())).Returns(Task.CompletedTask);
        venueRepo.Setup(r => r.GetByEspnIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new Venue { Id = 100, EspnId = id, Name = id });

        var service = new ApiSportsGameService(
            httpClient, logger, providerSettings, CreateRateLimiter(),
            gameRepo.Object, teamRepo.Object, teamSeasonRepo.Object, venueRepo.Object);

        return (service, gameRepo, teamRepo, teamSeasonRepo);
    }

    private static void SetupTeamLookups(Mock<ITeamRepository> teamRepo, Mock<ITeamSeasonRepository> teamSeasonRepo)
    {
        var abbrToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["WAS"] = 1, ["IND"] = 2, ["BUF"] = 3, ["NE"] = 4, ["CHI"] = 5, ["NYJ"] = 6,
            ["CIN"] = 7, ["JAX"] = 8, ["NYG"] = 9, ["ARI"] = 10, ["PHI"] = 11, ["LAR"] = 12,
            ["TB"] = 13, ["GB"] = 14, ["BAL"] = 15, ["TEN"] = 16, ["HOU"] = 17, ["DAL"] = 18,
            ["MIN"] = 19, ["MIA"] = 20, ["LV"] = 21, ["KC"] = 22, ["SF"] = 23, ["DEN"] = 24,
            ["SEA"] = 25, ["LAC"] = 26
        };

        foreach (var (abbr, id) in abbrToId)
        {
            teamRepo.Setup(r => r.GetByAbbreviationAsync(abbr))
                .ReturnsAsync(new Team { Id = id, Abbreviation = abbr, Name = abbr });
            teamSeasonRepo.Setup(r => r.EnsureFromTeamAsync(It.Is<Team>(t => t.Abbreviation == abbr), 2026))
                .ReturnsAsync(new TeamSeason { Id = id * 10, Abbreviation = abbr, Season = 2026 });
        }
    }

    [Fact]
    public async Task ScrapeGamesAsync_Week4_ShouldSkipNcaaAndUpsertThirteenNflGames()
    {
        var json = await File.ReadAllTextAsync(FixturePath("games-by-date.json"));
        var handler = new FakeHttpHandler(json);
        var (service, gameRepo, teamRepo, teamSeasonRepo) = CreateService(handler);
        SetupTeamLookups(teamRepo, teamSeasonRepo);

        var result = await service.ScrapeGamesAsync(2026, 4);

        Assert.True(result.Success);
        Assert.Equal(13, result.RecordsProcessed);
        gameRepo.Verify(r => r.UpsertAsync(It.IsAny<Game>()), Times.Exactly(13));
    }

    [Fact]
    public async Task ScrapeGamesAsync_LondonGame_ResolvesCommandersAsHome()
    {
        var json = await File.ReadAllTextAsync(FixturePath("games-by-date.json"));
        var handler = new FakeHttpHandler(json);
        var (service, gameRepo, teamRepo, teamSeasonRepo) = CreateService(handler);
        SetupTeamLookups(teamRepo, teamSeasonRepo);

        Game? londonGame = null;
        gameRepo.Setup(r => r.UpsertAsync(It.IsAny<Game>()))
            .Callback<Game>(g =>
            {
                if (g.DataSourceRecordId == "21562")
                    londonGame = g;
            })
            .Returns(Task.CompletedTask);

        await service.ScrapeGamesAsync(2026, 4);

        Assert.NotNull(londonGame);
        Assert.Equal(10, londonGame.HomeTeamSeasonId);
        Assert.Equal(20, londonGame.AwayTeamSeasonId);
    }

    [Fact]
    public async Task ScrapeGamesAsync_ScheduledNflGame_StoresNullScores()
    {
        var json = await File.ReadAllTextAsync(FixturePath("games-by-date.json"));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.Clone();
        var response = root.GetProperty("response");
        var nflScheduled = """
        {
          "game": {
            "id": 99999, "stage": "Regular Season", "week": "Week 4",
            "date": { "timezone": "UTC", "date": "2026-10-05", "time": "17:00", "timestamp": 1791219600 },
            "venue": { "name": "Test Stadium", "city": "Test City" },
            "status": { "short": "NS", "long": "Not Started", "timer": null }
          },
          "league": { "id": 1, "name": "NFL", "season": "2026" },
          "teams": { "home": { "id": 20, "name": "Buffalo Bills" }, "away": { "id": 3, "name": "New England Patriots" } },
          "scores": {
            "home": { "quarter_1": null, "quarter_2": null, "quarter_3": null, "quarter_4": null, "overtime": null, "total": null },
            "away": { "quarter_1": null, "quarter_2": null, "quarter_3": null, "quarter_4": null, "overtime": null, "total": null }
          }
        }
        """;

        var list = new List<JsonElement>();
        foreach (var item in response.EnumerateArray())
            list.Add(item.Clone());
        list.Add(JsonDocument.Parse(nflScheduled).RootElement);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("get", "games");
            writer.WritePropertyName("parameters");
            writer.WriteStartObject();
            writer.WriteString("season", "2026");
            writer.WriteEndObject();
            writer.WritePropertyName("errors");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WriteNumber("results", list.Count);
            writer.WritePropertyName("response");
            writer.WriteStartArray();
            foreach (var item in list)
                item.WriteTo(writer);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        var modifiedJson = Encoding.UTF8.GetString(stream.ToArray());
        var handler = new FakeHttpHandler(modifiedJson);
        var (service, gameRepo, teamRepo, teamSeasonRepo) = CreateService(handler);
        SetupTeamLookups(teamRepo, teamSeasonRepo);

        Game? scheduled = null;
        gameRepo.Setup(r => r.UpsertAsync(It.IsAny<Game>()))
            .Callback<Game>(g =>
            {
                if (g.DataSourceRecordId == "99999")
                    scheduled = g;
            })
            .Returns(Task.CompletedTask);

        var result = await service.ScrapeGamesAsync(2026, 4);

        Assert.True(result.Success);
        Assert.Equal(14, result.RecordsProcessed);
        Assert.NotNull(scheduled);
        Assert.Null(scheduled.HomeScore);
        Assert.Null(scheduled.AwayScore);
        Assert.Null(scheduled.HomeQ1);
        Assert.Equal("Scheduled", scheduled.GameStatus);
    }

    [Fact]
    public async Task ScrapeGamesAsync_UnknownTeamId_SkipsGameWithoutFailingBatch()
    {
        var json = await File.ReadAllTextAsync(FixturePath("games-by-date.json"));
        using var doc = JsonDocument.Parse(json);
        var extra = """
        {
          "game": { "id": 88888, "stage": "Regular Season", "week": "Week 4",
            "date": { "timestamp": 1791219600 },
            "venue": null,
            "status": { "short": "FT" } },
          "league": { "id": 1, "name": "NFL", "season": "2026" },
          "teams": { "home": { "id": 888, "name": "Mystery" }, "away": { "id": 20, "name": "Buffalo Bills" } },
          "scores": { "home": { "total": 0 }, "away": { "total": 7 } }
        }
        """;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("get", "games");
            writer.WritePropertyName("errors");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WriteNumber("results", doc.RootElement.GetProperty("response").GetArrayLength() + 1);
            writer.WritePropertyName("response");
            writer.WriteStartArray();
            foreach (var item in doc.RootElement.GetProperty("response").EnumerateArray())
                item.WriteTo(writer);
            JsonDocument.Parse(extra).RootElement.WriteTo(writer);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        var handler = new FakeHttpHandler(Encoding.UTF8.GetString(stream.ToArray()));
        var (service, gameRepo, teamRepo, teamSeasonRepo) = CreateService(handler);
        SetupTeamLookups(teamRepo, teamSeasonRepo);

        var result = await service.ScrapeGamesAsync(2026, 4);

        Assert.True(result.Success);
        Assert.Equal(13, result.RecordsProcessed);
    }

    [Fact]
    public async Task ScrapeGamesAsync_EnvelopeError_ReturnsFailed()
    {
        var errorJson = """{"get":"games","parameters":{},"errors":{"token":"Error/Missing application key"},"results":0,"response":[]}""";
        var handler = new FakeHttpHandler(errorJson);
        var (service, _, teamRepo, teamSeasonRepo) = CreateService(handler);
        SetupTeamLookups(teamRepo, teamSeasonRepo);

        var result = await service.ScrapeGamesAsync(2026, 4);

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
