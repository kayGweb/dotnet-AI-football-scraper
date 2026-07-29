using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebScraper.Api.Auth;
using WebScraper.Api.Dtos.Admin;
using WebScraper.Services.Backup;

namespace WebScraper.Api.Controllers;

[ApiController]
[Route("api/v1/backup")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
[Produces("application/json")]
public class BackupController : ControllerBase
{
    private readonly SqliteBackupService _backupService;
    private readonly ILogger<BackupController> _logger;

    public BackupController(SqliteBackupService backupService, ILogger<BackupController> logger)
    {
        _backupService = backupService;
        _logger = logger;
    }

    /// <summary>Create a timestamped copy of the local SQLite database.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SqliteBackupCreatedDto), StatusCodes.Status201Created)]
    public ActionResult<SqliteBackupCreatedDto> CreateBackup(CancellationToken ct)
    {
        var result = _backupService.CreateBackup(ct);
        _logger.LogInformation("SQLite backup created: {Path}", result.Path);

        return Created(string.Empty, new SqliteBackupCreatedDto
        {
            FileName = result.FileName,
            Path = result.Path,
            SizeBytes = result.SizeBytes,
            PrunedCount = result.PrunedCount,
        });
    }

    /// <summary>List existing SQLite backups (newest first).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SqliteBackupDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<SqliteBackupDto>> ListBackups()
    {
        var backups = _backupService.ListBackups()
            .Select(b => new SqliteBackupDto
            {
                FileName = b.FileName,
                Path = b.Path,
                SizeBytes = b.SizeBytes,
                CreatedAtUtc = b.CreatedAtUtc,
            })
            .ToList();

        return Ok(backups);
    }
}
