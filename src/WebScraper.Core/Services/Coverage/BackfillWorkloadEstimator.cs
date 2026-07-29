namespace WebScraper.Services.Coverage;

/// <summary>
/// Runtime estimates for ESPN-provider backfills at the configured request delay.
/// See AGENT_PLATFORM_PLAN.md §0.
/// </summary>
public static class BackfillWorkloadEstimator
{
    public const int DefaultRequestDelayMs = 1500;

    /// <summary>Documented scoreboard calls for the 20-year (2006–2025) ESPN backfill.</summary>
    public const int PlanTwentyYearScoreboardCalls = 450;

    /// <summary>Documented game-summary calls for the 20-year (2006–2025) ESPN backfill.</summary>
    public const int PlanTwentyYearSummaryCalls = 5430;

    /// <summary>Documented total API calls for the 20-year (2006–2025) ESPN backfill.</summary>
    public const int PlanTwentyYearTotalApiCalls = 5900;

    /// <summary>Documented expected game rows for the 20-year (2006–2025) load.</summary>
    public const int PlanTwentyYearGameCount = 5432;

    public static BackfillWorkloadEstimate Estimate(
        int startSeason,
        int endSeason,
        int requestDelayMs = DefaultRequestDelayMs)
    {
        if (requestDelayMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestDelayMs), "Request delay must be positive.");

        var scoreboardCalls = NflSeasonSchedule.GetScoreboardCalls(startSeason, endSeason);
        var summaryCalls = NflSeasonSchedule.GetTotalGameCount(startSeason, endSeason);
        var totalApiCalls = scoreboardCalls + summaryCalls;
        var wallClock = TimeSpan.FromMilliseconds((long)totalApiCalls * requestDelayMs);

        return new BackfillWorkloadEstimate(scoreboardCalls, summaryCalls, totalApiCalls, wallClock);
    }

    public static BackfillWorkloadEstimate EstimateTwentyYearPlan(int requestDelayMs = DefaultRequestDelayMs)
    {
        var wallClock = TimeSpan.FromMilliseconds((long)PlanTwentyYearTotalApiCalls * requestDelayMs);
        return new BackfillWorkloadEstimate(
            PlanTwentyYearScoreboardCalls,
            PlanTwentyYearSummaryCalls,
            PlanTwentyYearTotalApiCalls,
            wallClock);
    }
}

public sealed record BackfillWorkloadEstimate(
    int ScoreboardCalls,
    int SummaryCalls,
    int TotalApiCalls,
    TimeSpan EstimatedWallClock);
