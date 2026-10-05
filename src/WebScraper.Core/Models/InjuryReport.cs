namespace WebScraper.Models;

/// <summary>
/// Point-in-time injury snapshot from a provider (e.g. api-sports), distinct from per-game <see cref="Injury"/> rows.
/// </summary>
public class InjuryReport : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public int TeamSeasonId { get; set; }
    public int? PlayerId { get; set; }
    public string ExternalPlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? ReportedAt { get; set; }
    public DateTime SnapshotAt { get; set; }

    // Data lineage
    public string? DataSource { get; set; }
    public DateTime? DataSourceFetchedAt { get; set; }
    public string? DataSourceRecordId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public string? DeleteReason { get; set; }

    public TeamSeason TeamSeason { get; set; } = null!;
    public Player? Player { get; set; }
}
