using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Services;
using WebScraper.Services.Scrapers;

namespace WebScraper.Tests.Scrapers;

public class PlayerScraperParsingTests
{
    private static readonly ScraperSettings TestSettings = new()
    {
        RequestDelayMs = 0,
        MaxRetries = 1,
        UserAgent = "Test/1.0",
        TimeoutSeconds = 5
    };

    private static readonly string RosterHtml = @"
        <html><body>
        <table id='roster'>
            <tbody>
                <tr>
                    <th data-stat='jersey_number'>15</th>
                    <td data-stat='player'><a href='/players/M/MahoPa00.htm'>Patrick Mahomes</a></td>
                    <td data-stat='pos'>QB</td>
                    <td data-stat='height'>6-3</td>
                    <td data-stat='weight'>230</td>
                    <td data-stat='college'>Texas Tech</td>
                </tr>
                <tr>
                    <th data-stat='jersey_number'>87</th>
                    <td data-stat='player'><a href='/players/K/KelcTr00.htm'>Travis Kelce</a></td>
                    <td data-stat='pos'>TE</td>
                    <td data-stat='height'>6-5</td>
                    <td data-stat='weight'>250</td>
                    <td data-stat='college'>Cincinnati</td>
                </tr>
            </tbody>
        </table>
        </body></html>";

    private static readonly Team TestTeam = new()
    {
        Id = 1,
        Name = "Kansas City Chiefs",
        Abbreviation = "KC",
        City = "Kansas City",
        Conference = "AFC",
        Division = "West"
    };

    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        public string? LastRequestUrl { get; private set; }

        public FakeHttpHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, System.Text.Encoding.UTF8, "text/html")
            });
        }
    }

    private static (PlayerScraperService scraper, Mock<IPlayerRepository> playerRepo, Mock<ITeamRepository> teamRepo, FakeHttpHandler handler)
        CreateScraper(string html = "")
    {
        var handler = new FakeHttpHandler(string.IsNullOrEmpty(html) ? RosterHtml : html);
        var httpClient = new HttpClient(handler);
        var logger = NullLogger<PlayerScraperService>.Instance;
        var settings = Options.Create(TestSettings);
        var rateLimiter = new RateLimiterService(Options.Create(new ScraperSettings { RequestDelayMs = 0 }));
        var playerRepo = new Mock<IPlayerRepository>();
        var teamRepo = new Mock<ITeamRepository>();

        var scraper = new PlayerScraperService(httpClient, logger, settings, rateLimiter, playerRepo.Object, teamRepo.Object);
        return (scraper, playerRepo, teamRepo, handler);
    }

    [Fact]
    public void ParsePlayerNode_ShouldExtractPlayerData_FromValidHtml()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(RosterHtml);

        var row = doc.DocumentNode.SelectSingleNode("//table[@id='roster']//tbody//tr[1]");
        Assert.NotNull(row);

        var (scraper, _, _, _) = CreateScraper();
        var method = typeof(PlayerScraperService).GetMethod("ParsePlayerNode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var result = method.Invoke(scraper, new object[] { row, 1 }) as Player;

        Assert.NotNull(result);
        Assert.Equal("Patrick Mahomes", result.Name);
        Assert.Equal("QB", result.Position);
        Assert.Equal(15, result.JerseyNumber);
        Assert.Equal("6-3", result.Height);
        Assert.Equal(230, result.Weight);
        Assert.Equal("Texas Tech", result.College);
        Assert.Equal(1, result.TeamId);
    }

    [Fact]
    public async Task ScrapePlayersAsync_ByAbbreviation_ShouldUpsertPlayers_WhenTeamFound()
    {
        var (scraper, playerRepo, teamRepo, _) = CreateScraper();
        teamRepo.Setup(r => r.GetByAbbreviationAsync("KC")).ReturnsAsync(TestTeam);
        teamRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(TestTeam);

        await scraper.ScrapePlayersAsync("KC");

        playerRepo.Verify(r => r.UpsertAsync(It.Is<Player>(p => p.Name == "Patrick Mahomes")), Times.Once);
        playerRepo.Verify(r => r.UpsertAsync(It.Is<Player>(p => p.Name == "Travis Kelce")), Times.Once);
        playerRepo.Verify(r => r.UpsertAsync(It.IsAny<Player>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ScrapePlayersAsync_ByAbbreviation_ShouldNotUpsert_WhenTeamNotFound()
    {
        var (scraper, playerRepo, teamRepo, _) = CreateScraper();
        teamRepo.Setup(r => r.GetByAbbreviationAsync("INVALID")).ReturnsAsync((Team?)null);

        await scraper.ScrapePlayersAsync("INVALID");

        playerRepo.Verify(r => r.UpsertAsync(It.IsAny<Player>()), Times.Never);
    }

    [Fact]
    public async Task ScrapePlayersAsync_WithSeason_ShouldUseSeasonInUrl()
    {
        var (scraper, _, teamRepo, handler) = CreateScraper();
        teamRepo.Setup(r => r.GetByAbbreviationAsync("KC")).ReturnsAsync(TestTeam);
        teamRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(TestTeam);

        await scraper.ScrapePlayersAsync("KC", 2023);

        Assert.NotNull(handler.LastRequestUrl);
        Assert.Contains("/teams/kan/2023_roster.htm", handler.LastRequestUrl);
    }

    [Fact]
    public async Task ScrapePlayersAsync_WithoutSeason_ShouldUseCurrentYearInUrl()
    {
        var (scraper, _, teamRepo, handler) = CreateScraper();
        teamRepo.Setup(r => r.GetByAbbreviationAsync("KC")).ReturnsAsync(TestTeam);
        teamRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(TestTeam);

        await scraper.ScrapePlayersAsync("KC");

        Assert.NotNull(handler.LastRequestUrl);
        Assert.Contains($"/teams/kan/{DateTime.Now.Year}_roster.htm", handler.LastRequestUrl);
    }

    [Fact]
    public async Task ScrapeAllPlayersAsync_WithSeason_ShouldScrapeAllTeamsWithSeason()
    {
        var team2 = new Team { Id = 2, Name = "Dallas Cowboys", Abbreviation = "DAL", City = "Dallas", Conference = "NFC", Division = "East" };
        var (scraper, playerRepo, teamRepo, handler) = CreateScraper();
        teamRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Team> { TestTeam, team2 });
        teamRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(TestTeam);
        teamRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(team2);

        await scraper.ScrapeAllPlayersAsync(2023);

        // Both teams should have players upserted (2 players per team from the roster HTML)
        playerRepo.Verify(r => r.UpsertAsync(It.IsAny<Player>()), Times.Exactly(4));
    }

    [Fact]
    public void GetPfrAbbreviation_ShouldMapKnownAbbreviations()
    {
        var method = typeof(PlayerScraperService).GetMethod("GetPfrAbbreviation",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        Assert.Equal("kan", method.Invoke(null, new object[] { "KC" }));
        Assert.Equal("crd", method.Invoke(null, new object[] { "ARI" }));
        Assert.Equal("rav", method.Invoke(null, new object[] { "BAL" }));
        Assert.Equal("nwe", method.Invoke(null, new object[] { "NE" }));
    }

    [Fact]
    public void GetPfrAbbreviation_ShouldPassThroughUnmappedAbbreviations()
    {
        var method = typeof(PlayerScraperService).GetMethod("GetPfrAbbreviation",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        Assert.Equal("dal", method.Invoke(null, new object[] { "DAL" }));
        Assert.Equal("chi", method.Invoke(null, new object[] { "CHI" }));
        Assert.Equal("buf", method.Invoke(null, new object[] { "BUF" }));
    }
}
