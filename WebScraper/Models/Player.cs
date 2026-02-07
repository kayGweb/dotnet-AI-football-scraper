namespace WebScraper.Models;

public class Player
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public string Position { get; set; } = string.Empty;
    public int? JerseyNumber { get; set; }
    public string? Height { get; set; }
    public int? Weight { get; set; }
    public string? College { get; set; }

    // Navigation properties
    public Team? Team { get; set; }
    public ICollection<PlayerGameStats> GameStats { get; set; } = new List<PlayerGameStats>();
}
