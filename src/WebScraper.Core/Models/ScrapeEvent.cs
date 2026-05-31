namespace WebScraper.Models;

/// <summary>
/// Outbox event written transactionally with ScrapeJob state changes. The
/// ScrapeEventRelay BackgroundService polls this table in ID order and
/// broadcasts events to SignalR subscribers. The monotonic Id also drives
/// the replay endpoint so clients can catch up after reconnecting.
/// </summary>
public class ScrapeEvent
{
    public long Id { get; set; }

    public int JobId { get; set; }

    public ScrapeEventType EventType { get; set; }

    public DateTime Timestamp { get; set; }

    /// <summary>JSON payload with event-specific fields (status, error, counts).</summary>
    public string Payload { get; set; } = "{}";
}

public enum ScrapeEventType
{
    JobQueued,
    JobStarted,
    JobCompleted,
    JobFailed
}
