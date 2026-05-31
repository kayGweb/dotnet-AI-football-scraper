namespace WebScraper.Api.Dtos;

public class GameDto
{
    public int Id { get; set; }
    public int Season { get; set; }
    public int Week { get; set; }
    public DateTime GameDate { get; set; }
    public string? GameStatus { get; set; }
    public string? EspnEventId { get; set; }

    public TeamSummaryDto HomeTeam { get; set; } = new();
    public TeamSummaryDto AwayTeam { get; set; } = new();
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public bool? HomeWinner { get; set; }

    public int? Attendance { get; set; }
    public bool NeutralSite { get; set; }
    public VenueSummaryDto? Venue { get; set; }

    public QuarterScoresDto? QuarterScores { get; set; }

    public MetaDto Meta { get; set; } = new();
}

public class TeamSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
}

public class VenueSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public bool IsIndoor { get; set; }
}

public class QuarterScoresDto
{
    public int? HomeQ1 { get; set; }
    public int? HomeQ2 { get; set; }
    public int? HomeQ3 { get; set; }
    public int? HomeQ4 { get; set; }
    public int? HomeOT { get; set; }
    public int? AwayQ1 { get; set; }
    public int? AwayQ2 { get; set; }
    public int? AwayQ3 { get; set; }
    public int? AwayQ4 { get; set; }
    public int? AwayOT { get; set; }
}
