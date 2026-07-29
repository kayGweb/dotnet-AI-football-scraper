using WebScraper.Models;
using WebScraper.Services.Coverage;

namespace WebScraper.Services.Coverage;

/// <summary>
/// A single unit of work in a multi-season backfill (one scoreboard week or one stats week).
/// </summary>
public sealed record BackfillWorkItem(
    int Season,
    NflSeasonType SeasonType,
    int Week,
    ScrapeJobType JobType);

/// <summary>
/// Generates ordered backfill work items for fan-out into child <see cref="ScrapeJob"/> rows.
/// </summary>
public static class BackfillPlanner
{
    public static IReadOnlyList<BackfillWorkItem> Plan(
        int startSeason,
        int endSeason,
        bool includePreseason = true,
        bool includePostseason = true)
    {
        if (endSeason < startSeason)
            throw new ArgumentOutOfRangeException(nameof(endSeason));

        var items = new List<BackfillWorkItem>();

        for (var season = endSeason; season >= startSeason; season--)
        {
            var seasonTypes = new List<NflSeasonType> { NflSeasonType.Regular };
            if (includePreseason)
                seasonTypes.Insert(0, NflSeasonType.Preseason);
            if (includePostseason)
                seasonTypes.Add(NflSeasonType.Postseason);

            foreach (var seasonType in seasonTypes)
            {
                var weeks = NflSeasonSchedule.GetScoreboardWeeks(seasonType, season);
                for (var week = 1; week <= weeks; week++)
                {
                    items.Add(new BackfillWorkItem(season, seasonType, week, ScrapeJobType.Games));
                    items.Add(new BackfillWorkItem(season, seasonType, week, ScrapeJobType.Stats));
                }
            }
        }

        return items;
    }
}
