namespace WebScraper.Models;

public class ScrapeResult
{
    public bool Success { get; set; }
    public int RecordsProcessed { get; set; }
    public int RecordsFailed { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();

    public static ScrapeResult Succeeded(int count, string message) => new()
    {
        Success = true,
        RecordsProcessed = count,
        Message = message
    };

    public static ScrapeResult Failed(string message) => new()
    {
        Success = false,
        Message = message
    };

    public static ScrapeResult Failed(string message, List<string> errors) => new()
    {
        Success = false,
        Message = message,
        Errors = errors
    };
}
