using WebScraper.Models;
using WebScraper.Services.Coverage;

namespace WebScraper.Tests.Services;

public class NflSeasonScheduleTests
{
    [Theory]
    [InlineData(2006, 256, 11, 267)]
    [InlineData(2019, 256, 11, 267)]
    [InlineData(2020, 256, 13, 269)]
    [InlineData(2021, 272, 13, 285)]
    [InlineData(2025, 272, 13, 285)]
    public void GetTotalGameCount_MatchesEraRules(int season, int regular, int playoff, int total)
    {
        Assert.Equal(regular, NflSeasonSchedule.GetRegularSeasonGameCount(season));
        Assert.Equal(playoff, NflSeasonSchedule.GetPlayoffGameCount(season));
        Assert.Equal(total, NflSeasonSchedule.GetTotalGameCount(season));
    }

    [Fact]
    public void GetTotalGameCount_2006Through2025_EqualsPlanTotal()
    {
        var total = NflSeasonSchedule.GetTotalGameCount(
            NflSeasonSchedule.TwentyYearBackfillStartSeason,
            NflSeasonSchedule.TwentyYearBackfillEndSeason);

        Assert.Equal(BackfillWorkloadEstimator.PlanTwentyYearGameCount, total);
    }

    [Fact]
    public void GetTotalGameCount_2006Through2025_BreaksDownByEra()
    {
        Assert.Equal(14 * 267, NflSeasonSchedule.GetTotalGameCount(2006, 2019));
        Assert.Equal(269, NflSeasonSchedule.GetTotalGameCount(2020, 2020));
        Assert.Equal(5 * 285, NflSeasonSchedule.GetTotalGameCount(2021, 2025));
    }

    [Theory]
    [InlineData(NflSeasonType.Preseason, 2006, 4)]
    [InlineData(NflSeasonType.Regular, 2006, 17)]
    [InlineData(NflSeasonType.Regular, 2021, 18)]
    [InlineData(NflSeasonType.Postseason, 2025, 4)]
    public void GetScoreboardWeeks_MatchesSeasonTypeAndEra(
        NflSeasonType seasonType,
        int season,
        int expectedWeeks)
    {
        Assert.Equal(expectedWeeks, NflSeasonSchedule.GetScoreboardWeeks(seasonType, season));
    }

    [Fact]
    public void GetScoreboardCalls_2006Through2025_IsPositiveAndBelowManualJobExplosion()
    {
        var calls = NflSeasonSchedule.GetScoreboardCalls(2006, 2025);

        Assert.InRange(calls, 400, 700);
    }
}
