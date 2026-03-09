namespace WebScraper.Models;

public class ApiLink
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string EndpointType { get; set; } = string.Empty;
    public string RelationType { get; set; } = string.Empty;
    public int? GameId { get; set; }
    public int? TeamId { get; set; }
    public int? Season { get; set; }
    public int? Week { get; set; }
    public string? EspnEventId { get; set; }
    public DateTime DiscoveredAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }

    public Game? Game { get; set; }
    public Team? Team { get; set; }
}
