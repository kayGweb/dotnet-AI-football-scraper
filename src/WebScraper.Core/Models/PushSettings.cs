namespace WebScraper.Models;

public class PushSettings
{
    /// <summary>Rows per batch for large tables (games, player stats).</summary>
    public int BatchSize { get; set; } = 500;
}

public class PushOptions
{
    public bool Resume { get; set; }

    public bool Reset { get; set; }

    public int? BatchSize { get; set; }

    public static PushOptions Default => new();
}
