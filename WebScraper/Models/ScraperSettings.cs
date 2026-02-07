namespace WebScraper.Models;

public class ScraperSettings
{
    public int RequestDelayMs { get; set; } = 1500;
    public int MaxRetries { get; set; } = 3;
    public string UserAgent { get; set; } = "NFLScraper/1.0 (educational project)";
    public int TimeoutSeconds { get; set; } = 30;
}
