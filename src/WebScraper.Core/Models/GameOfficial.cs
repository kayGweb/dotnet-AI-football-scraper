namespace WebScraper.Models;

public class GameOfficial : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public string? DataSource { get; set; }
    public DateTime? DataSourceFetchedAt { get; set; }
    public string? DataSourceRecordId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public string? DeleteReason { get; set; }

    public Game Game { get; set; } = null!;
}
