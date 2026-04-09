namespace WebScraper.Models;

public class Injury : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int? PlayerId { get; set; }
    public string EspnAthleteId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string InjuryType { get; set; } = string.Empty;
    public string BodyLocation { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTime? ReturnDate { get; set; }
    public DateTime ReportDate { get; set; }

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

    public Game Game { get; set; } = null!;
    public Player? Player { get; set; }
}
