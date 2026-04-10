namespace WebScraper.Api.Dtos;

/// <summary>
/// High-level database status snapshot returned from <c>GET /api/v1/status</c>.
/// Used by Claude and dashboards to sanity-check the data store.
/// </summary>
public class StatusDto
{
    public int Teams { get; set; }
    public int Players { get; set; }
    public int Games { get; set; }
    public int PlayerGameStats { get; set; }
    public int Venues { get; set; }
    public int TeamGameStats { get; set; }
    public int Injuries { get; set; }
    public int ApiLinks { get; set; }
    public DateTime? LatestUpdate { get; set; }
    public string ApiVersion { get; set; } = "v1";
}
