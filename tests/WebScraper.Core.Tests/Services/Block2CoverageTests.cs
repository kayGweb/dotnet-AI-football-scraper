using WebScraper.Models;
using WebScraper.Services.Coverage;

namespace WebScraper.Tests.Services;

public class NflSeasonScheduleWeekTests
{
    [Theory]
    [InlineData(NflSeasonType.Preseason, 2025, 1, 16)]
    [InlineData(NflSeasonType.Preseason, 2025, 4, 8)]
    [InlineData(NflSeasonType.Postseason, 2025, 1, 6)]
    [InlineData(NflSeasonType.Postseason, 2019, 1, 4)]
    [InlineData(NflSeasonType.Postseason, 2025, 4, 1)]
    public void GetExpectedGamesForWeek_ReturnsDeterministicCounts(
        NflSeasonType seasonType, int season, int week, int expected)
    {
        Assert.Equal(expected, NflSeasonSchedule.GetExpectedGamesForWeek(seasonType, season, week));
    }

    [Fact]
    public void GetExpectedGamesForWeek_RegularSeason_ReturnsNull()
    {
        Assert.Null(NflSeasonSchedule.GetExpectedGamesForWeek(NflSeasonType.Regular, 2025, 5));
    }
}

public class BackfillDependencyTests
{
    [Fact]
    public void Plan_AlternatesGamesThenStatsPerWeek()
    {
        var items = BackfillPlanner.Plan(2025, 2025, includePreseason: false, includePostseason: false);

        var regularWeek1 = items
            .Where(i => i.SeasonType == NflSeasonType.Regular && i.Week == 1)
            .ToList();

        Assert.Equal(2, regularWeek1.Count);
        Assert.Equal(ScrapeJobType.Games, regularWeek1[0].JobType);
        Assert.Equal(ScrapeJobType.Stats, regularWeek1[1].JobType);
    }
}
