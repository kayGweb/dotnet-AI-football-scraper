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
    public double? QBRating { get; set; }
    public double? AdjQBR { get; set; }
    public int Sacks { get; set; }
    public int SackYardsLost { get; set; }

    // Rushing
    public int RushAttempts { get; set; }
    public int RushYards { get; set; }
    public int RushTouchdowns { get; set; }
    public int LongRushing { get; set; }

    // Receiving
    public int Receptions { get; set; }
    public int ReceivingYards { get; set; }
    public int ReceivingTouchdowns { get; set; }
    public int ReceivingTargets { get; set; }
    public int LongReception { get; set; }
    public double YardsPerReception { get; set; }

    // Fumbles
    public int Fumbles { get; set; }
    public int FumblesLost { get; set; }
    public int FumblesRecovered { get; set; }

    // Defensive
    public int TotalTackles { get; set; }
    public int SoloTackles { get; set; }
    public double DefensiveSacks { get; set; }
    public int TacklesForLoss { get; set; }
    public int PassesDefended { get; set; }
    public int QBHits { get; set; }
    public int DefensiveTouchdowns { get; set; }

    // Interceptions (defensive)
    public int InterceptionsCaught { get; set; }
    public int InterceptionYards { get; set; }
    public int InterceptionTouchdowns { get; set; }

    // Kick returns
    public int KickReturns { get; set; }
    public int KickReturnYards { get; set; }
    public int LongKickReturn { get; set; }
    public int KickReturnTouchdowns { get; set; }

    // Punt returns
    public int PuntReturns { get; set; }
    public int PuntReturnYards { get; set; }
    public int LongPuntReturn { get; set; }
    public int PuntReturnTouchdowns { get; set; }

    // Kicking
    public int FieldGoalsMade { get; set; }
    public int FieldGoalAttempts { get; set; }
    public int LongFieldGoal { get; set; }
    public int ExtraPointsMade { get; set; }
    public int ExtraPointAttempts { get; set; }
    public int TotalKickingPoints { get; set; }

    // Punting
    public int Punts { get; set; }
    public int PuntYards { get; set; }
    public double GrossAvgPuntYards { get; set; }
    public int PuntTouchbacks { get; set; }
    public int PuntsInside20 { get; set; }
    public int LongPunt { get; set; }

    // Navigation properties
    public Player Player { get; set; } = null!;
    public Game Game { get; set; } = null!;
}
