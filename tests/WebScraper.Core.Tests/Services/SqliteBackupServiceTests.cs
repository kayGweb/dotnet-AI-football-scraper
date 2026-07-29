using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using WebScraper.Models;
using WebScraper.Services.Backup;

namespace WebScraper.Tests.Services;

public class SqliteBackupServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly string _backupDir;

    public SqliteBackupServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"nfl-backup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "nfl_data.db");
        _backupDir = Path.Combine(_tempDir, "backups");
        File.WriteAllText(_dbPath, "test-db-content");
    }

    [Fact]
    public void CreateBackup_CopiesDatabase_AndPrunesOldFiles()
    {
        var service = CreateService(retainCount: 2);

        service.CreateBackup();
        service.CreateBackup();
        var third = service.CreateBackup();

        Assert.True(File.Exists(third.Path));
        Assert.Equal("test-db-content", File.ReadAllText(third.Path));

        var backups = service.ListBackups();
        Assert.Equal(2, backups.Count);
    }

    [Fact]
    public void ListBackups_ReturnsNewestFirst()
    {
        var service = CreateService();
        service.CreateBackup();
        Thread.Sleep(10);
        service.CreateBackup();

        var backups = service.ListBackups();
        Assert.Equal(2, backups.Count);
        Assert.True(backups[0].CreatedAtUtc >= backups[1].CreatedAtUtc);
    }

    private SqliteBackupService CreateService(int retainCount = 3)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}",
            })
            .Build();

        var settings = Options.Create(new BackupSettings
        {
            BackupDirectory = _backupDir,
            RetainCount = retainCount,
        });

        return new SqliteBackupService(config, settings);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
