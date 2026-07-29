namespace WebScraper.Models;

public class ScoringPlay : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string EspnPlayId { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public int? TeamSeasonId { get; set; }
    public int Period { get; set; }
    public string? Clock { get; set; }
    public string PlayType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public string? ScoringType { get; set; }

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
    public TeamSeason? TeamSeason { get; set; }
}
