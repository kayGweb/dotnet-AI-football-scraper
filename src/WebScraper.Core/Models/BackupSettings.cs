namespace WebScraper.Models;

public class BackupSettings
{
    /// <summary>Directory for SQLite backup copies (relative to repo root).</summary>
    public string BackupDirectory { get; set; } = "data/backups";

    /// <summary>Number of backup files to retain (oldest pruned).</summary>
    public int RetainCount { get; set; } = 3;
}
