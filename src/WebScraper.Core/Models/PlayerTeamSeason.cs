namespace WebScraper.Models;

/// <summary>
/// Player membership on a team for a given season (many-to-many with season context).
/// </summary>
public class PlayerTeamSeason : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public int TeamSeasonId { get; set; }
    public int Season { get; set; }

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

    public Player Player { get; set; } = null!;
    public TeamSeason TeamSeason { get; set; } = null!;
}
