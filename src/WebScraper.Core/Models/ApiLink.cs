namespace WebScraper.Models;

public class ApiLink : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string EndpointType { get; set; } = string.Empty;
    public string RelationType { get; set; } = string.Empty;
    public int? GameId { get; set; }
    public int? TeamId { get; set; }
    public int? Season { get; set; }
    public int? Week { get; set; }
    public string? EspnEventId { get; set; }
    public DateTime DiscoveredAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }

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

    public Game? Game { get; set; }
    public Team? Team { get; set; }
}
