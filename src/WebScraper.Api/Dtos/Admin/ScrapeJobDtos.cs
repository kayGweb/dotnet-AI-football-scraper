using WebScraper.Models;

namespace WebScraper.Api.Dtos.Admin;

public class CreateScrapeJobRequest
{
    public int? Season { get; set; }
    public int? Week { get; set; }
}

public class ScrapeJobDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int? Season { get; set; }
    public int? Week { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RecordsProcessed { get; set; }
    public int RecordsFailed { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? RequestedBy { get; set; }
}

public static class ScrapeJobMappings
{
    public static ScrapeJobDto ToDto(this ScrapeJob job) => new()
    {
        Id = job.Id,
        Type = job.Type.ToString(),
        Source = job.Source,
        Season = job.Season,
        Week = job.Week,
        Status = job.Status.ToString(),
        RecordsProcessed = job.RecordsProcessed,
        RecordsFailed = job.RecordsFailed,
        Error = job.Error,
        CreatedAt = job.CreatedAt,
        StartedAt = job.StartedAt,
        CompletedAt = job.CompletedAt,
        RequestedBy = job.RequestedBy,
    };
}
