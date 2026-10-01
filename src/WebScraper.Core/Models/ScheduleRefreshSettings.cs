namespace WebScraper.Models;

/// <summary>
/// Configuration for the scheduled refresh of the current season's schedule/scores.
/// </summary>
public class ScheduleRefreshSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>How often to enqueue a games scrape for the current season (default: every 12h).</summary>
    public int IntervalHours { get; set; } = 12;
}
