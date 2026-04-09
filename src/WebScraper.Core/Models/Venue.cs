namespace WebScraper.Models;

public class Venue : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public string EspnId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool IsGrass { get; set; }
    public bool IsIndoor { get; set; }

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

    public ICollection<Game> Games { get; set; } = new List<Game>();
}
