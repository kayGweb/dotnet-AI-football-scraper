namespace WebScraper.Models;

public class GameDrive : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string EspnDriveId { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public int? TeamSeasonId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? StartPeriod { get; set; }
    public int? EndPeriod { get; set; }
    public string? TimeElapsed { get; set; }
    public int Yards { get; set; }
    public int OffensivePlays { get; set; }
    public bool IsScore { get; set; }
    public string? Result { get; set; }
    public string? DisplayResult { get; set; }

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
