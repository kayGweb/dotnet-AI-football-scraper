namespace WebScraper.Models;

public class Game
{
    public int Id { get; set; }
    public int Season { get; set; }
    public int Week { get; set; }
    public DateTime GameDate { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }

    // Navigation properties
    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;
    public ICollection<PlayerGameStats> PlayerStats { get; set; } = new List<PlayerGameStats>();
}
