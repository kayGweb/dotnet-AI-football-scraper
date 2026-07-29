using WebScraper.Models;

namespace WebScraper.Services.Coverage;

/// <summary>
/// Era-aware NFL schedule math used for coverage targets and backfill planning.
/// See AGENT_PLATFORM_PLAN.md §0 and §4.1.
/// </summary>
public static class NflSeasonSchedule
{
    public const int TwentyYearBackfillStartSeason = 2006;
    public const int TwentyYearBackfillEndSeason = 2025;

    public static int GetRegularSeasonGameCount(int season) =>
        season >= 2021 ? 272 : 256;

    public static int GetPlayoffGameCount(int season) =>
        season >= 2020 ? 13 : 11;

    public static int GetTotalGameCount(int season) =>
        GetRegularSeasonGameCount(season) + GetPlayoffGameCount(season);

    public static int GetTotalGameCount(int startSeason, int endSeason)
    {
        if (endSeason < startSeason)
            throw new ArgumentOutOfRangeException(nameof(endSeason), "End season must be >= start season.");

        var total = 0;
        for (var season = startSeason; season <= endSeason; season++)
            total += GetTotalGameCount(season);

        return total;
    }

    public static int GetScoreboardWeeks(NflSeasonType seasonType, int season) =>
        seasonType switch
        {
            NflSeasonType.Preseason => 4,
            NflSeasonType.Regular => season >= 2021 ? 18 : 17,
            NflSeasonType.Postseason => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(seasonType))
        };

    public static int GetScoreboardCalls(int season)
    {
        var total = 0;
        foreach (NflSeasonType seasonType in Enum.GetValues<NflSeasonType>())
            total += GetScoreboardWeeks(seasonType, season);

        return total;
    }

    public static int GetScoreboardCalls(int startSeason, int endSeason)
    {
        if (endSeason < startSeason)
            throw new ArgumentOutOfRangeException(nameof(endSeason), "End season must be >= start season.");

        var total = 0;
        for (var season = startSeason; season <= endSeason; season++)
            total += GetScoreboardCalls(season);

        return total;
    }

    /// <summary>
    /// Expected games for a single scoreboard week when determinable (preseason/postseason).
    /// Regular-season weeks return null because bye weeks make per-week counts variable.
    /// </summary>
    public static int? GetExpectedGamesForWeek(NflSeasonType seasonType, int season, int week)
    {
        return seasonType switch
        {
            NflSeasonType.Preseason => week switch
            {
                1 or 2 or 3 => 16,
                4 => 8,
                _ => null,
            },
            NflSeasonType.Regular => null,
            NflSeasonType.Postseason => GetExpectedPlayoffGamesForWeek(season, week),
            _ => null,
        };
    }

    private static int? GetExpectedPlayoffGamesForWeek(int season, int week)
    {
        var wildCardGames = season >= 2020 ? 6 : 4;

        return week switch
        {
            1 => wildCardGames,
            2 => 4,
            3 => 2,
            4 => 1,
            _ => null,
        };
    }
}
