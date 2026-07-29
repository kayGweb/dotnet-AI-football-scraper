using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using WebScraper.Extensions;
using WebScraper.Models;

namespace WebScraper.Services.Backup;

/// <summary>
/// Copies the local SQLite database to a timestamped backup file and prunes old copies.
/// Phase E requires backing up before each backfill session (AGENT_PLATFORM_PLAN.md §7).
/// </summary>
public class SqliteBackupService
{
    private readonly IConfiguration _configuration;
    private readonly BackupSettings _settings;

    public SqliteBackupService(IConfiguration configuration, IOptions<BackupSettings> settings)
    {
        _configuration = configuration;
        _settings = settings.Value;
    }

    public SqliteBackupResult CreateBackup(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var connectionString = ServiceCollectionExtensions.ResolveSqlitePath(
            _configuration.GetConnectionString("DefaultConnection"));

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("DefaultConnection is not configured.");

        var sourcePath = ExtractSqlitePath(connectionString);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"SQLite database not found: {sourcePath}");

        var anchor = FindRepositoryRoot() ?? Directory.GetCurrentDirectory();
        var backupDir = Path.IsPathRooted(_settings.BackupDirectory)
            ? _settings.BackupDirectory
            : Path.Combine(anchor, _settings.BackupDirectory);

        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        var backupFileName = $"nfl_data_{timestamp}.db";
        var backupPath = Path.Combine(backupDir, backupFileName);

        File.Copy(sourcePath, backupPath, overwrite: false);

        var pruned = PruneOldBackups(backupDir, _settings.RetainCount, backupPath);

        return new SqliteBackupResult(backupPath, backupFileName, new FileInfo(backupPath).Length, pruned);
    }

    public IReadOnlyList<SqliteBackupInfo> ListBackups()
    {
        var anchor = FindRepositoryRoot() ?? Directory.GetCurrentDirectory();
        var backupDir = Path.IsPathRooted(_settings.BackupDirectory)
            ? _settings.BackupDirectory
            : Path.Combine(anchor, _settings.BackupDirectory);

        if (!Directory.Exists(backupDir))
            return Array.Empty<SqliteBackupInfo>();

        return Directory.GetFiles(backupDir, "nfl_data_*.db")
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new SqliteBackupInfo(info.Name, info.FullName, info.Length, info.CreationTimeUtc);
            })
            .OrderByDescending(b => b.CreatedAtUtc)
            .ToList();
    }

    private static int PruneOldBackups(string backupDir, int retainCount, string? neverDeletePath = null)
    {
        if (retainCount <= 0)
            return 0;

        var files = Directory.GetFiles(backupDir, "nfl_data_*.db")
            .Select(path => new FileInfo(path))
            .OrderByDescending(f => f.CreationTimeUtc)
            .ThenByDescending(f => f.Name)
            .ToList();

        var pruned = 0;
        foreach (var file in files.Skip(retainCount))
        {
            if (neverDeletePath is not null &&
                string.Equals(file.FullName, neverDeletePath, StringComparison.OrdinalIgnoreCase))
                continue;

            file.Delete();
            pruned++;
        }

        return pruned;
    }

    private static string ExtractSqlitePath(string connectionString)
    {
        const string prefix = "Data Source=";
        if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only SQLite Data Source= connection strings are supported.");

        return connectionString[prefix.Length..].Trim();
    }

    private static string? FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (dir.EnumerateFiles("*.sln").Any() || Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }
}

public sealed record SqliteBackupResult(string Path, string FileName, long SizeBytes, int PrunedCount);

public sealed record SqliteBackupInfo(string FileName, string Path, long SizeBytes, DateTime CreatedAtUtc);
