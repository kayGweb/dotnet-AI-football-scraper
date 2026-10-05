using System.Text.Json;
using WebScraper.Models;
using WebScraper.Services.Scrapers.ApiSports;

namespace WebScraper.Tests.Scrapers.ApiSports;

public class ApiSportsMappingsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiSports", fileName);

    [Theory]
    [InlineData(1, "LV")]
    [InlineData(2, "JAX")]
    [InlineData(3, "NE")]
    [InlineData(4, "NYG")]
    [InlineData(5, "BAL")]
    [InlineData(6, "TEN")]
    [InlineData(10, "CIN")]
    [InlineData(11, "ARI")]
    [InlineData(12, "PHI")]
    [InlineData(13, "NYJ")]
    [InlineData(14, "SF")]
    [InlineData(15, "GB")]
    [InlineData(16, "CHI")]
    [InlineData(17, "KC")]
    [InlineData(18, "WAS")]
    [InlineData(20, "BUF")]
    [InlineData(21, "IND")]
    [InlineData(23, "SEA")]
    [InlineData(24, "TB")]
    [InlineData(25, "MIA")]
    [InlineData(26, "HOU")]
    [InlineData(28, "DEN")]
    [InlineData(29, "DAL")]
    [InlineData(30, "LAC")]
    [InlineData(31, "LAR")]
    [InlineData(32, "MIN")]
    public void TeamIdToAbbreviation_KnownIdsFromPlan(int teamId, string expected)
    {
        Assert.Equal(expected, ApiSportsMappings.TeamIdToAbbreviation(teamId));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(19)]
    [InlineData(22)]
    [InlineData(27)]
    [InlineData(999)]
    public void TeamIdToAbbreviation_UnknownIdsReturnNull(int teamId)
    {
        Assert.Null(ApiSportsMappings.TeamIdToAbbreviation(teamId));
    }

    [Fact]
    public void GamesFixture_EveryNflTeamIdResolves()
    {
        var json = File.ReadAllText(FixturePath("games-by-date.json"));
        var envelope = JsonSerializer.Deserialize<ApiSportsEnvelope<ApiSportsGameItem>>(json, JsonOptions)!;

        var teamIds = envelope.Response
            .Where(ApiSportsMappings.IsNfl)
            .SelectMany(g => new[] { g.Teams.Home.Id, g.Teams.Away.Id })
            .Distinct();

        foreach (var id in teamIds)
        {
            Assert.NotNull(ApiSportsMappings.TeamIdToAbbreviation(id));
        }
    }

    [Fact]
    public void ParseWeek_RegularSeasonWeek4()
    {
        var result = ApiSportsMappings.ParseWeek("Regular Season", "Week 4");
        Assert.NotNull(result);
        Assert.Equal(NflSeasonType.Regular, result.Value.SeasonType);
        Assert.Equal(4, result.Value.Week);
    }

    [Fact]
    public void ParseWeek_PreSeason()
    {
        var result = ApiSportsMappings.ParseWeek("Pre Season", "Week 2");
        Assert.NotNull(result);
        Assert.Equal(NflSeasonType.Preseason, result.Value.SeasonType);
        Assert.Equal(2, result.Value.Week);
    }

    [Theory]
    [InlineData("Wild Card", 1)]
    [InlineData("Divisional Round", 2)]
    [InlineData("Conference Championships", 3)]
    [InlineData("Super Bowl", 4)]
    public void ParseWeek_PostSeasonRounds(string weekLabel, int expectedWeek)
    {
        var result = ApiSportsMappings.ParseWeek("Post Season", weekLabel);
        Assert.NotNull(result);
        Assert.Equal(NflSeasonType.Postseason, result.Value.SeasonType);
        Assert.Equal(expectedWeek, result.Value.Week);
    }

    [Theory]
    [InlineData("FBS (Division I-A)", "5")]
    [InlineData("Regular Season", "4")]
    public void ParseWeek_NonNflOrBareWeek_ReturnsNull(string stage, string week)
    {
        Assert.Null(ApiSportsMappings.ParseWeek(stage, week));
    }

    [Theory]
    [InlineData("FT", "Final", true, false)]
    [InlineData("AOT", "Final", true, false)]
    [InlineData("NS", "Scheduled", false, true)]
    [InlineData("Q1", "In Progress", false, false)]
    [InlineData("Q2", "In Progress", false, false)]
    [InlineData("Q3", "In Progress", false, false)]
    [InlineData("Q4", "In Progress", false, false)]
    [InlineData("HT", "In Progress", false, false)]
    [InlineData("OT", "In Progress", false, false)]
    [InlineData("CANC", "Cancelled", false, false)]
    [InlineData("PST", "Postponed", false, false)]
    public void ParseStatus_MapsShortCodes(
        string shortCode,
        string expectedStatus,
        bool expectedFinal,
        bool expectedScheduled)
    {
        var (status, isFinal, isScheduled) = ApiSportsMappings.ParseStatus(shortCode);
        Assert.Equal(expectedStatus, status);
        Assert.Equal(expectedFinal, isFinal);
        Assert.Equal(expectedScheduled, isScheduled);
    }

    [Theory]
    [InlineData("5' 8\"", "5-8")]
    [InlineData("6' 1\"", "6-1")]
    [InlineData("6' 8\"", "6-8")]
    public void ParseHeight_ValidStrings(string input, string expected)
    {
        Assert.Equal(expected, ApiSportsMappings.ParseHeight(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tall")]
    public void ParseHeight_InvalidReturnsNull(string? input)
    {
        Assert.Null(ApiSportsMappings.ParseHeight(input));
    }

    [Theory]
    [InlineData("203 lbs", 203)]
    [InlineData("325 lbs", 325)]
    [InlineData("204 lbs", 204)]
    public void ParseWeight_ValidStrings(string input, int expected)
    {
        Assert.Equal(expected, ApiSportsMappings.ParseWeight(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("heavy")]
    public void ParseWeight_InvalidReturnsNull(string? input)
    {
        Assert.Null(ApiSportsMappings.ParseWeight(input));
    }

    [Fact]
    public void IsNfl_OnlyLeagueOne()
    {
        var json = File.ReadAllText(FixturePath("games-by-date.json"));
        var envelope = JsonSerializer.Deserialize<ApiSportsEnvelope<ApiSportsGameItem>>(json, JsonOptions)!;

        Assert.Equal(13, envelope.Response.Count(ApiSportsMappings.IsNfl));
        Assert.All(envelope.Response.Where(g => !ApiSportsMappings.IsNfl(g)),
            g => Assert.Equal(2, g.League.Id));
    }
}
