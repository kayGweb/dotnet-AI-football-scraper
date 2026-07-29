using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebScraper.Api.Auth;
using WebScraper.Api.Dtos.Admin;
using WebScraper.Api.Services;
using WebScraper.Data;
using WebScraper.Models;
using WebScraper.Services.Backup;
using WebScraper.Services.Coverage;

namespace WebScraper.Api.Controllers;

[ApiController]
[Route("api/v1/backfill")]
[Authorize(Policy = AuthorizationPolicies.RequireOperate)]
[Produces("application/json")]
public class BackfillController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IJobQueue _queue;
    private readonly BackfillOrchestrator _orchestrator;
    private readonly SqliteBackupService _backupService;
    private readonly ScraperSettings _scraperSettings;
    private readonly ILogger<BackfillController> _logger;

    public BackfillController(
        AppDbContext db,
        IJobQueue queue,
        BackfillOrchestrator orchestrator,
        SqliteBackupService backupService,
        IOptions<ScraperSettings> scraperSettings,
        ILogger<BackfillController> logger)
    {
        _db = db;
        _queue = queue;
        _orchestrator = orchestrator;
        _backupService = backupService;
        _scraperSettings = scraperSettings.Value;
        _logger = logger;
    }

    /// <summary>Estimate API calls and wall-clock time for a season range.</summary>
    [HttpGet("estimate")]
    [ProducesResponseType(typeof(BackfillWorkloadDto), StatusCodes.Status200OK)]
    public ActionResult<BackfillWorkloadDto> GetEstimate(
        [FromQuery] int startSeason = 2006,
        [FromQuery] int endSeason = 2025)
    {
        if (endSeason < startSeason)
            return BadRequest(new { message = "endSeason must be >= startSeason." });

        return Ok(BackfillWorkloadDto.From(BackfillWorkloadEstimator.Estimate(startSeason, endSeason)));
    }

    /// <summary>Start a multi-season backfill. Optionally backs up SQLite first.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ScrapeJobDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Start([FromBody] StartBackfillRequest request, CancellationToken ct)
    {
        if (request.EndSeason < request.StartSeason)
        {
            return Problem(
                title: "Invalid season range",
                detail: "EndSeason must be greater than or equal to StartSeason.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.BackupFirst)
        {
            try
            {
                var backup = _backupService.CreateBackup(ct);
                _logger.LogInformation("Pre-backfill backup created: {Path}", backup.Path);
            }
            catch (Exception ex)
            {
                return Problem(
                    title: "Backup failed",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        var job = new ScrapeJob
        {
            Type = ScrapeJobType.Backfill,
            Source = _scraperSettings.DataProvider,
            Season = request.StartSeason,
            Week = request.EndSeason,
            Status = ScrapeJobStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            RequestedBy = User.FindFirstValue(ClaimTypes.Email),
        };

        _db.ScrapeJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        _db.ScrapeEvents.Add(new ScrapeEvent
        {
            JobId = job.Id,
            EventType = ScrapeEventType.JobQueued,
            Timestamp = DateTime.UtcNow,
            Payload = JsonSerializer.Serialize(new
            {
                type = job.Type.ToString(),
                startSeason = request.StartSeason,
                endSeason = request.EndSeason,
                backupFirst = request.BackupFirst,
                requestedBy = job.RequestedBy,
            }),
        });
        await _db.SaveChangesAsync(ct);

        _queue.TryEnqueue(job.Id);

        return AcceptedAtAction(nameof(JobsController.GetJob), "Jobs", new { id = job.Id }, job.ToDto());
    }

    /// <summary>Aggregate progress for a backfill parent job.</summary>
    [HttpGet("{id:int}/progress")]
    [ProducesResponseType(typeof(BackfillProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BackfillProgressDto>> GetProgress(int id, CancellationToken ct)
    {
        var parent = await _db.ScrapeJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct);
        if (parent is null || parent.Type != ScrapeJobType.Backfill)
            return NotFound();

        var progress = await _orchestrator.GetProgressAsync(id, ct);
        var estimate = BackfillWorkloadEstimator.Estimate(progress.StartSeason, progress.EndSeason);
        return Ok(BackfillProgressDto.From(progress, estimate));
    }

    /// <summary>Pause a running backfill — queued child jobs will not dequeue until resumed.</summary>
    [HttpPost("{id:int}/pause")]
    [ProducesResponseType(typeof(ScrapeJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ScrapeJobDto>> Pause(int id, CancellationToken ct)
    {
        var job = await _db.ScrapeJobs.FindAsync(new object[] { id }, ct);
        if (job is null || job.Type != ScrapeJobType.Backfill)
            return NotFound();

        if (job.Status is not (ScrapeJobStatus.Running or ScrapeJobStatus.Queued))
            return BadRequest(new { message = "Only running or queued backfills can be paused." });

        job.Status = ScrapeJobStatus.Paused;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Backfill job {JobId} paused by {User}", id, User.Identity?.Name);
        return Ok(job.ToDto());
    }

    /// <summary>Resume a paused backfill and re-enqueue ready child jobs.</summary>
    [HttpPost("{id:int}/resume")]
    [ProducesResponseType(typeof(ScrapeJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ScrapeJobDto>> Resume(int id, CancellationToken ct)
    {
        var job = await _db.ScrapeJobs.FindAsync(new object[] { id }, ct);
        if (job is null || job.Type != ScrapeJobType.Backfill)
            return NotFound();

        if (job.Status != ScrapeJobStatus.Paused)
            return BadRequest(new { message = "Only paused backfills can be resumed." });

        job.Status = ScrapeJobStatus.Running;
        job.StartedAt ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var children = await _db.ScrapeJobs
            .Where(j => j.ParentJobId == id)
            .ToListAsync(ct);

        foreach (var childId in BackfillProgressCalculator.GetEnqueueableChildIds(children))
            _queue.TryEnqueue(childId);

        _logger.LogInformation("Backfill job {JobId} resumed by {User}", id, User.Identity?.Name);
        return Ok(job.ToDto());
    }
}
