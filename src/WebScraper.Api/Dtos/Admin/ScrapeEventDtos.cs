namespace WebScraper.Api.Dtos.Admin;

public class ScrapeEventDto
{
    public long Id { get; set; }
    public int JobId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Payload { get; set; } = "{}";
}

public static class ScrapeEventMappings
{
    public static ScrapeEventDto ToDto(this WebScraper.Models.ScrapeEvent evt) => new()
    {
        Id = evt.Id,
        JobId = evt.JobId,
        EventType = evt.EventType.ToString(),
        Timestamp = evt.Timestamp,
        Payload = evt.Payload,
    };
}
