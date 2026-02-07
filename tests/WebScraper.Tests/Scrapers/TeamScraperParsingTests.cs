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

public class TeamScraperParsingTests
{
    private static TeamScraperService CreateScraper(ITeamRepository? repo = null)
    {
        var httpClient = new HttpClient();
        var logger = NullLogger<TeamScraperService>.Instance;
        var settings = Options.Create(new ScraperSettings
        {
            RequestDelayMs = 0,
            MaxRetries = 1,
            UserAgent = "Test/1.0",
            TimeoutSeconds = 5
        });
        var rateLimiter = new RateLimiterService(Options.Create(new ScraperSettings { RequestDelayMs = 0 }));
        var teamRepo = repo ?? new Mock<ITeamRepository>().Object;

        return new TeamScraperService(httpClient, logger, settings, rateLimiter, teamRepo);
    }

    [Fact]
    public void ParseTeamNode_ShouldExtractTeamData_FromValidHtml()
    {
        // Simulate a PFR teams_active table row
        var html = @"
        <table id='teams_active'>
            <tbody>
                <tr>
                    <th data-stat='team_name'><a href='/teams/kan/'>Kansas City Chiefs</a></th>
                    <td data-stat='years'>50</td>
                </tr>
            </tbody>
        </table>";

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var row = doc.DocumentNode.SelectSingleNode("//tbody//tr");
        Assert.NotNull(row);

        // Use reflection to call private ParseTeamNode
        var scraper = CreateScraper();
        var method = typeof(TeamScraperService).GetMethod("ParseTeamNode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var result = method.Invoke(scraper, new object[] { row }) as Team;

        Assert.NotNull(result);
        Assert.Equal("Kansas City Chiefs", result.Name);
        Assert.Equal("KC", result.Abbreviation);
        Assert.Equal("Kansas City", result.City);
        Assert.Equal("AFC", result.Conference);
        Assert.Equal("West", result.Division);
    }

    [Fact]
    public void ParseTeamNode_ShouldReturnNull_ForHeaderRow()
    {
        var html = @"
        <table id='teams_active'>
            <tbody>
                <tr class='thead'>
                    <th>Team</th>
                </tr>
            </tbody>
        </table>";

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var row = doc.DocumentNode.SelectSingleNode("//tbody//tr");
        Assert.NotNull(row);

        var scraper = CreateScraper();
        var method = typeof(TeamScraperService).GetMethod("ParseTeamNode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var result = method.Invoke(scraper, new object[] { row }) as Team;
        Assert.Null(result);
    }

    [Fact]
    public void ParseTeamNode_ShouldReturnNull_WhenNoLink()
    {
        var html = @"
        <table>
            <tbody>
                <tr>
                    <th data-stat='team_name'>No Link Here</th>
                </tr>
            </tbody>
        </table>";

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var row = doc.DocumentNode.SelectSingleNode("//tbody//tr");

        var scraper = CreateScraper();
        var method = typeof(TeamScraperService).GetMethod("ParseTeamNode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var result = method!.Invoke(scraper, new object[] { row! }) as Team;
        Assert.Null(result);
    }

    [Fact]
    public void ExtractCity_ShouldHandleTwoWordCities()
    {
        var method = typeof(TeamScraperService).GetMethod("ExtractCity",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        Assert.Equal("New England", method.Invoke(null, new object[] { "New England Patriots" }));
        Assert.Equal("Kansas City", method.Invoke(null, new object[] { "Kansas City Chiefs" }));
        Assert.Equal("San Francisco", method.Invoke(null, new object[] { "San Francisco 49ers" }));
        Assert.Equal("Green Bay", method.Invoke(null, new object[] { "Green Bay Packers" }));
    }

    [Fact]
    public void ExtractCity_ShouldHandleSingleWordCities()
    {
        var method = typeof(TeamScraperService).GetMethod("ExtractCity",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.Equal("Dallas", method!.Invoke(null, new object[] { "Dallas Cowboys" }));
        Assert.Equal("Chicago", method.Invoke(null, new object[] { "Chicago Bears" }));
    }
}
