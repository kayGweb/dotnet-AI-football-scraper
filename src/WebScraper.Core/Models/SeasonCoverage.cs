namespace WebScraper.Models;

/// <summary>
/// Expected-vs-actual coverage snapshot for one scoreboard week.
/// Refreshed by <see cref="Services.Coverage.SeasonCoverageService"/>.
/// </summary>
public class SeasonCoverage
{
    public int Id { get; set; }

    public int Season { get; set; }

    public NflSeasonType SeasonType { get; set; }

    public int Week { get; set; }

    /// <summary>Expected games for this week (null when not determinable, e.g. regular-season bye weeks).</summary>
    public int? ExpectedGames { get; set; }

    public int ActualGames { get; set; }

    public int GamesWithPlayerStats { get; set; }

    public int GamesWithTeamStats { get; set; }

    public int GamesWithInjuries { get; set; }

    public int GamesWithOdds { get; set; }

    public int PlayerCount { get; set; }

    public DateTime? LastVerifiedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
