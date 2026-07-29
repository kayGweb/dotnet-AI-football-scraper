namespace WebScraper.Models;

/// <summary>
/// Configuration for the scheduled odds polling job (§5.1).
/// </summary>
public class OddsPollSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>How often to enqueue an odds-poll scrape job (default: daily).</summary>
    public int IntervalHours { get; set; } = 24;

    /// <summary>Poll games with kickoff up to this many days in the future.</summary>
    public int LookAheadDays { get; set; } = 14;

    /// <summary>Also poll games that kicked off within this many days (for closing lines).</summary>
    public int LookBackDays { get; set; } = 3;
}
