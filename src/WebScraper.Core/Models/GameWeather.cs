namespace WebScraper.Models;

public class GameWeather : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int? TemperatureF { get; set; }
    public int? HighTemperatureF { get; set; }
    public string? Condition { get; set; }
    public int? WindSpeedMph { get; set; }
    public string? WindDirection { get; set; }
    public int? HumidityPercent { get; set; }

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
