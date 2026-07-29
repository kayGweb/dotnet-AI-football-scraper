namespace WebScraper.Models;

/// <summary>
/// Stable NFL franchise identity across relocations and rebrandings.
/// Games reference <see cref="TeamSeason"/> rows, not this table directly.
/// </summary>
public class Franchise : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }

    /// <summary>Current canonical abbreviation (e.g. LAR, LAC, LV).</summary>
    public string CanonicalAbbreviation { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

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

    public ICollection<TeamSeason> TeamSeasons { get; set; } = new List<TeamSeason>();
}
