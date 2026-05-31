namespace WebScraper.Models;

public class ScrapeJob
{
    public int Id { get; set; }

    public ScrapeJobType Type { get; set; }

    public string Source { get; set; } = string.Empty;

    public int? Season { get; set; }

    public int? Week { get; set; }

    public ScrapeJobStatus Status { get; set; } = ScrapeJobStatus.Queued;

    public int RecordsProcessed { get; set; }

    public int RecordsFailed { get; set; }

    public string? Error { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? RequestedBy { get; set; }
}

public enum ScrapeJobType
{
    Teams,
    Players,
    Games,
    Stats,
    All
}

public enum ScrapeJobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed
}
