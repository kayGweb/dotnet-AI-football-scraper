using WebScraper.Models;

namespace WebScraper.Services.Push;

internal static class DatabasePushCopiers
{
    public static void CopyAllPlayerStats(PlayerGameStats source, PlayerGameStats target)
    {
        target.PassAttempts = source.PassAttempts;
        target.PassCompletions = source.PassCompletions;
        target.PassYards = source.PassYards;
        target.PassTouchdowns = source.PassTouchdowns;
        target.Interceptions = source.Interceptions;
        target.QBRating = source.QBRating;
        target.AdjQBR = source.AdjQBR;
        target.Sacks = source.Sacks;
        target.SackYardsLost = source.SackYardsLost;
        target.RushAttempts = source.RushAttempts;
        target.RushYards = source.RushYards;
        target.RushTouchdowns = source.RushTouchdowns;
        target.LongRushing = source.LongRushing;
        target.Receptions = source.Receptions;
        target.ReceivingYards = source.ReceivingYards;
        target.ReceivingTouchdowns = source.ReceivingTouchdowns;
        target.ReceivingTargets = source.ReceivingTargets;
        target.LongReception = source.LongReception;
        target.YardsPerReception = source.YardsPerReception;
        target.Fumbles = source.Fumbles;
        target.FumblesLost = source.FumblesLost;
        target.FumblesRecovered = source.FumblesRecovered;
        target.TotalTackles = source.TotalTackles;
        target.SoloTackles = source.SoloTackles;
        target.DefensiveSacks = source.DefensiveSacks;
        target.TacklesForLoss = source.TacklesForLoss;
        target.PassesDefended = source.PassesDefended;
        target.QBHits = source.QBHits;
        target.DefensiveTouchdowns = source.DefensiveTouchdowns;
        target.InterceptionsCaught = source.InterceptionsCaught;
        target.InterceptionYards = source.InterceptionYards;
        target.InterceptionTouchdowns = source.InterceptionTouchdowns;
        target.KickReturns = source.KickReturns;
        target.KickReturnYards = source.KickReturnYards;
        target.LongKickReturn = source.LongKickReturn;
        target.KickReturnTouchdowns = source.KickReturnTouchdowns;
        target.PuntReturns = source.PuntReturns;
        target.PuntReturnYards = source.PuntReturnYards;
        target.LongPuntReturn = source.LongPuntReturn;
        target.PuntReturnTouchdowns = source.PuntReturnTouchdowns;
        target.FieldGoalsMade = source.FieldGoalsMade;
        target.FieldGoalAttempts = source.FieldGoalAttempts;
        target.LongFieldGoal = source.LongFieldGoal;
        target.ExtraPointsMade = source.ExtraPointsMade;
        target.ExtraPointAttempts = source.ExtraPointAttempts;
        target.TotalKickingPoints = source.TotalKickingPoints;
        target.Punts = source.Punts;
        target.PuntYards = source.PuntYards;
        target.GrossAvgPuntYards = source.GrossAvgPuntYards;
        target.PuntTouchbacks = source.PuntTouchbacks;
        target.PuntsInside20 = source.PuntsInside20;
        target.LongPunt = source.LongPunt;
    }

    public static void CopyAllTeamGameStats(TeamGameStats source, TeamGameStats target)
    {
        target.FirstDowns = source.FirstDowns;
        target.FirstDownsPassing = source.FirstDownsPassing;
        target.FirstDownsRushing = source.FirstDownsRushing;
        target.FirstDownsPenalty = source.FirstDownsPenalty;
        target.ThirdDownMade = source.ThirdDownMade;
        target.ThirdDownAttempts = source.ThirdDownAttempts;
        target.FourthDownMade = source.FourthDownMade;
        target.FourthDownAttempts = source.FourthDownAttempts;
        target.TotalPlays = source.TotalPlays;
        target.TotalYards = source.TotalYards;
        target.NetPassingYards = source.NetPassingYards;
        target.PassCompletions = source.PassCompletions;
        target.PassAttempts = source.PassAttempts;
        target.YardsPerPass = source.YardsPerPass;
        target.InterceptionsThrown = source.InterceptionsThrown;
        target.SacksAgainst = source.SacksAgainst;
        target.SackYardsLost = source.SackYardsLost;
        target.RushingYards = source.RushingYards;
        target.RushingAttempts = source.RushingAttempts;
        target.YardsPerRush = source.YardsPerRush;
        target.RedZoneMade = source.RedZoneMade;
        target.RedZoneAttempts = source.RedZoneAttempts;
        target.Turnovers = source.Turnovers;
        target.FumblesLost = source.FumblesLost;
        target.Penalties = source.Penalties;
        target.PenaltyYards = source.PenaltyYards;
        target.DefensiveTouchdowns = source.DefensiveTouchdowns;
        target.PossessionTime = source.PossessionTime;
    }
}
