namespace WebScraper.Models;

public class GameOdds : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string Sportsbook { get; set; } = string.Empty;
    public double? Spread { get; set; }
    public double? OverUnder { get; set; }
    public int? HomeMoneyline { get; set; }
    public int? AwayMoneyline { get; set; }
    public OddsSnapshotType SnapshotType { get; set; } = OddsSnapshotType.Current;
    public DateTime CapturedAt { get; set; }
    public string? Details { get; set; }

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

public enum OddsSnapshotType
{
    Opening = 1,
    Current = 2,
    Closing = 3,
}
