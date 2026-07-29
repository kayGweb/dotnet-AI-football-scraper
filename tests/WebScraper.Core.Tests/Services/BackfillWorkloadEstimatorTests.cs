using WebScraper.Services.Coverage;

namespace WebScraper.Tests.Services;

public class BackfillWorkloadEstimatorTests
{
    [Fact]
    public void EstimateTwentyYearPlan_MatchesDocumentedCallCounts()
    {
        var estimate = BackfillWorkloadEstimator.EstimateTwentyYearPlan();

        Assert.Equal(450, estimate.ScoreboardCalls);
        Assert.Equal(5430, estimate.SummaryCalls);
        Assert.Equal(5900, estimate.TotalApiCalls);
    }

    [Fact]
    public void EstimateTwentyYearPlan_AtDefaultDelay_IsAboutTwoAndHalfHours()
    {
        var estimate = BackfillWorkloadEstimator.EstimateTwentyYearPlan();

        Assert.InRange(estimate.EstimatedWallClock.TotalHours, 2.0, 3.0);
        Assert.Equal(TimeSpan.FromMilliseconds(5900L * 1500), estimate.EstimatedWallClock);
    }

    [Fact]
    public void Estimate_UsesComputedScoreboardCallsAndGameTotals()
    {
        var estimate = BackfillWorkloadEstimator.Estimate(2006, 2025);

        Assert.Equal(
            NflSeasonSchedule.GetScoreboardCalls(2006, 2025),
            estimate.ScoreboardCalls);
        Assert.Equal(
            BackfillWorkloadEstimator.PlanTwentyYearGameCount,
            estimate.SummaryCalls);
        Assert.Equal(
            estimate.ScoreboardCalls + estimate.SummaryCalls,
            estimate.TotalApiCalls);
    }

    [Fact]
    public void Estimate_SingleSeason_MatchesSeasonGameCount()
    {
        var estimate = BackfillWorkloadEstimator.Estimate(2020, 2020);

        Assert.Equal(269, estimate.SummaryCalls);
        Assert.Equal(NflSeasonSchedule.GetScoreboardCalls(2020), estimate.ScoreboardCalls);
    }

    [Fact]
    public void Estimate_RejectsInvalidRequestDelay()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BackfillWorkloadEstimator.Estimate(2006, 2025, requestDelayMs: 0));
    }
}
