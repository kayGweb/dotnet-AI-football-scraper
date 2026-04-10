namespace WebScraper.Api.Dtos;

/// <summary>
/// Flat snapshot of a player's performance in a single game, grouped by stat
/// category for readability. Maps 1:1 from <see cref="WebScraper.Models.PlayerGameStats"/>.
/// </summary>
public class PlayerGameStatsDto
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public int GameId { get; set; }
    public int Season { get; set; }
    public int Week { get; set; }

    public PassingStatsDto Passing { get; set; } = new();
    public RushingStatsDto Rushing { get; set; } = new();
    public ReceivingStatsDto Receiving { get; set; } = new();
    public FumblesStatsDto Fumbles { get; set; } = new();
    public DefensiveStatsDto Defensive { get; set; } = new();
    public InterceptionStatsDto Interceptions { get; set; } = new();
    public KickReturnStatsDto KickReturns { get; set; } = new();
    public PuntReturnStatsDto PuntReturns { get; set; } = new();
    public KickingStatsDto Kicking { get; set; } = new();
    public PuntingStatsDto Punting { get; set; } = new();

    public MetaDto Meta { get; set; } = new();
}

public class PassingStatsDto
{
    public int Attempts { get; set; }
    public int Completions { get; set; }
    public int Yards { get; set; }
    public int Touchdowns { get; set; }
    public int Interceptions { get; set; }
    public double? QbRating { get; set; }
    public double? AdjQbr { get; set; }
    public int Sacks { get; set; }
    public int SackYardsLost { get; set; }
}

public class RushingStatsDto
{
    public int Attempts { get; set; }
    public int Yards { get; set; }
    public int Touchdowns { get; set; }
    public int Long { get; set; }
}

public class ReceivingStatsDto
{
    public int Receptions { get; set; }
    public int Yards { get; set; }
    public int Touchdowns { get; set; }
    public int Targets { get; set; }
    public int Long { get; set; }
    public double YardsPerReception { get; set; }
}

public class FumblesStatsDto
{
    public int Fumbles { get; set; }
    public int Lost { get; set; }
    public int Recovered { get; set; }
}

public class DefensiveStatsDto
{
    public int TotalTackles { get; set; }
    public int SoloTackles { get; set; }
    public double Sacks { get; set; }
    public int TacklesForLoss { get; set; }
    public int PassesDefended { get; set; }
    public int QbHits { get; set; }
    public int Touchdowns { get; set; }
}

public class InterceptionStatsDto
{
    public int Caught { get; set; }
    public int Yards { get; set; }
    public int Touchdowns { get; set; }
}

public class KickReturnStatsDto
{
    public int Returns { get; set; }
    public int Yards { get; set; }
    public int Long { get; set; }
    public int Touchdowns { get; set; }
}

public class PuntReturnStatsDto
{
    public int Returns { get; set; }
    public int Yards { get; set; }
    public int Long { get; set; }
    public int Touchdowns { get; set; }
}

public class KickingStatsDto
{
    public int FieldGoalsMade { get; set; }
    public int FieldGoalAttempts { get; set; }
    public int LongFieldGoal { get; set; }
    public int ExtraPointsMade { get; set; }
    public int ExtraPointAttempts { get; set; }
    public int TotalKickingPoints { get; set; }
}

public class PuntingStatsDto
{
    public int Punts { get; set; }
    public int Yards { get; set; }
    public double GrossAverage { get; set; }
    public int Touchbacks { get; set; }
    public int Inside20 { get; set; }
    public int Long { get; set; }
}
