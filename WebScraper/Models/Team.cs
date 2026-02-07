namespace WebScraper.Models;

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Conference { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<Player> Players { get; set; } = new List<Player>();
    public ICollection<Game> HomeGames { get; set; } = new List<Game>();
    public ICollection<Game> AwayGames { get; set; } = new List<Game>();
}
