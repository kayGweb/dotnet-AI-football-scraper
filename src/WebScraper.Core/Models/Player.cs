namespace WebScraper.Models;

public class Player : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public string Position { get; set; } = string.Empty;
    public int? JerseyNumber { get; set; }
    public string? Height { get; set; }
    public int? Weight { get; set; }
    public string? College { get; set; }
    public string? EspnId { get; set; }

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

    // Navigation properties
    public Team? Team { get; set; }
    public ICollection<PlayerGameStats> GameStats { get; set; } = new List<PlayerGameStats>();
    public ICollection<Injury> Injuries { get; set; } = new List<Injury>();
}
