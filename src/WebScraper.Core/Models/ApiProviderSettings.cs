namespace WebScraper.Models;

public class ApiProviderSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string AuthType { get; set; } = "None";
    public string? AuthHeaderName { get; set; }
    public int RequestDelayMs { get; set; } = 1000;
    public Dictionary<string, string> CustomHeaders { get; set; } = new();
}
