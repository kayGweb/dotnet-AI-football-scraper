namespace WebScraper.Models;

/// <summary>
/// A data-quality assertion produced by <see cref="Services.Coverage.QualityRulesEngine"/>.
/// </summary>
public class DataQualityFinding
{
    public long Id { get; set; }

    public DataQualityRuleType RuleType { get; set; }

    public DataQualitySeverity Severity { get; set; }

    public DataQualityStatus Status { get; set; } = DataQualityStatus.Open;

    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    public int? Season { get; set; }

    public NflSeasonType? SeasonType { get; set; }

    public int? Week { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>JSON payload with repair hints (e.g. gameId, job type).</summary>
    public string? Payload { get; set; }

    public int? RepairJobId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }
}

public enum DataQualityRuleType
{
    GameMissingPlayerStats,
    QuarterScoresMismatch,
    GameMissingTeamStats,
    PlayerMissingEspnId,
    ImplausiblePassingYards,
    VenueMissingLocation,
    WeekGameCountMismatch,
}

public enum DataQualitySeverity
{
    Info,
    Warning,
    Error,
}

public enum DataQualityStatus
{
    Open,
    RepairQueued,
    Resolved,
    Dismissed,
}
