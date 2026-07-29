using WebScraper.Models;

namespace WebScraper.Api.Dtos.Admin;

public class DatabasePushSessionDto
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public int StageOffset { get; set; }
    public long TotalRecordsPushed { get; set; }
    public string? LastError { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public static DatabasePushSessionDto From(DatabasePushSession session) => new()
    {
        Id = session.Id,
        Status = session.Status.ToString(),
        CurrentStage = session.CurrentStage.ToString(),
        StageOffset = session.StageOffset,
        TotalRecordsPushed = session.TotalRecordsPushed,
        LastError = session.LastError,
        StartedAt = session.StartedAt,
        UpdatedAt = session.UpdatedAt,
        CompletedAt = session.CompletedAt,
    };
}
