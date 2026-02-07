namespace WebScraper.Models;

public class PlayerGameStats
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public int GameId { get; set; }

    // Passing
    public int PassAttempts { get; set; }
    public int PassCompletions { get; set; }
    public int PassYards { get; set; }
    public int PassTouchdowns { get; set; }
    public int Interceptions { get; set; }

    // Rushing
    public int RushAttempts { get; set; }
    public int RushYards { get; set; }
    public int RushTouchdowns { get; set; }

    // Receiving
    public int Receptions { get; set; }
    public int ReceivingYards { get; set; }
    public int ReceivingTouchdowns { get; set; }

    // Navigation properties
    public Player Player { get; set; } = null!;
    public Game Game { get; set; } = null!;
}
