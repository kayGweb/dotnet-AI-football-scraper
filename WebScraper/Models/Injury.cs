namespace WebScraper.Models;

public class Injury
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public int? PlayerId { get; set; }
    public string EspnAthleteId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string InjuryType { get; set; } = string.Empty;
    public string BodyLocation { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTime? ReturnDate { get; set; }
    public DateTime ReportDate { get; set; }

    public Game Game { get; set; } = null!;
    public Player? Player { get; set; }
}
