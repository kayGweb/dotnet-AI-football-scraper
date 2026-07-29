namespace WebScraper.Models;

/// <summary>
/// Agent-proposed field correction awaiting human approval (AGENT_PLATFORM_PLAN §2).
/// </summary>
public class DataCorrection
{
    public long Id { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public int EntityId { get; set; }

    public string Field { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string NewValue { get; set; } = string.Empty;

    public string Rationale { get; set; } = string.Empty;

    public string ProposedBy { get; set; } = string.Empty;

    public DataCorrectionStatus Status { get; set; } = DataCorrectionStatus.Pending;

    public DateTime CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public string? ResolvedBy { get; set; }
}

public enum DataCorrectionStatus
{
    Pending,
    Approved,
    Rejected,
    Applied,
}
