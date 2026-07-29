using WebScraper.Models;

namespace WebScraper.Api.Dtos.Admin;

public class SeasonCoverageDto
{
    public int Season { get; set; }
    public NflSeasonType SeasonType { get; set; }
    public int Week { get; set; }
    public int? ExpectedGames { get; set; }
    public int ActualGames { get; set; }
    public int GamesWithPlayerStats { get; set; }
    public int GamesWithTeamStats { get; set; }
    public int GamesWithInjuries { get; set; }
    public int PlayerCount { get; set; }
    public DateTime? LastVerifiedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class DataQualityFindingDto
{
    public long Id { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public int? Season { get; set; }
    public NflSeasonType? SeasonType { get; set; }
    public int? Week { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? RepairJobId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CoverageRefreshResultDto
{
    public int WeeksRefreshed { get; set; }
    public int OpenFindings { get; set; }
    public int RepairsEnqueued { get; set; }
}
