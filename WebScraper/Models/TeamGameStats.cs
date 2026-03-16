namespace WebScraper.Models;

public class TeamGameStats
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int TeamId { get; set; }

    // First downs
    public int FirstDowns { get; set; }
    public int FirstDownsPassing { get; set; }
    public int FirstDownsRushing { get; set; }
    public int FirstDownsPenalty { get; set; }

    // Efficiency
    public int ThirdDownMade { get; set; }
    public int ThirdDownAttempts { get; set; }
    public int FourthDownMade { get; set; }
    public int FourthDownAttempts { get; set; }

    // Offense totals
    public int TotalPlays { get; set; }
    public int TotalYards { get; set; }
    public int NetPassingYards { get; set; }
    public int PassCompletions { get; set; }
    public int PassAttempts { get; set; }
    public double YardsPerPass { get; set; }
    public int InterceptionsThrown { get; set; }
    public int SacksAgainst { get; set; }
    public int SackYardsLost { get; set; }
    public int RushingYards { get; set; }
    public int RushingAttempts { get; set; }
    public double YardsPerRush { get; set; }

    // Red zone
    public int RedZoneMade { get; set; }
    public int RedZoneAttempts { get; set; }

    // Turnovers & penalties
    public int Turnovers { get; set; }
    public int FumblesLost { get; set; }
    public int Penalties { get; set; }
    public int PenaltyYards { get; set; }
    public int DefensiveTouchdowns { get; set; }
    public string PossessionTime { get; set; } = string.Empty;

    public Game Game { get; set; } = null!;
    public Team Team { get; set; } = null!;
}
