using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebScraper.Api.Auth;
using WebScraper.Api.Dtos.Admin;
using WebScraper.Api.Pagination;
using WebScraper.Api.Services;
using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Api.Controllers;

/// <summary>
/// Read-only endpoints for inspecting scrape job status. Operators and admins
/// can view jobs; viewers get 403.
/// </summary>
[ApiController]
[Route("api/v1/jobs")]
[Authorize(Policy = AuthorizationPolicies.RequireOperate)]
[Produces("application/json")]
public class JobsController : ControllerBase
{
    private readonly AppDbContext _db;

    public JobsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>List scrape jobs (newest first), with optional status filter.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ScrapeJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ScrapeJobDto>>> ListJobs(
        [FromQuery] PaginationQuery pagination,
        [FromQuery] string? status = null)
    {
        var query = _db.ScrapeJobs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<ScrapeJobStatus>(status, ignoreCase: true, out var parsed))
        {
            query = query.Where(j => j.Status == parsed);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync();

        Response.Headers["X-Total-Count"] = total.ToString();

        return Ok(PagedResult<ScrapeJobDto>.From(
            items.Select(j => j.ToDto()).ToList(),
            pagination.Page,
            pagination.PageSize,
            total));
    }

    /// <summary>Get a single scrape job by ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ScrapeJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScrapeJobDto>> GetJob(int id)
    {
        var job = await _db.ScrapeJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id);
        if (job is null)
        {
            return Problem(
                title: "Job not found",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(job.ToDto());
    }

    /// <summary>List child jobs for a parent backfill job.</summary>
    [HttpGet("{id:int}/children")]
    [ProducesResponseType(typeof(IReadOnlyList<ScrapeJobDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ScrapeJobDto>>> GetChildren(int id)
    {
        var parent = await _db.ScrapeJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id);
        if (parent is null)
            return NotFound();

        var children = await _db.ScrapeJobs
            .AsNoTracking()
            .Where(j => j.ParentJobId == id)
            .OrderBy(j => j.Season)
            .ThenBy(j => j.SeasonType)
            .ThenBy(j => j.Week)
            .ThenBy(j => j.Type)
            .ToListAsync();

        return Ok(children.Select(j => j.ToDto()).ToList());
    }

    /// <summary>Re-queue a failed job for retry (idempotent re-scrape).</summary>
    [HttpPost("{id:int}/retry")]
    [ProducesResponseType(typeof(ScrapeJobDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RetryJob(int id, [FromServices] IJobQueue queue, CancellationToken ct)
    {
        var job = await _db.ScrapeJobs.FindAsync(new object[] { id }, ct);
        if (job is null)
            return NotFound();

        if (job.Status is not (ScrapeJobStatus.Failed or ScrapeJobStatus.Succeeded))
            return BadRequest(new { message = "Only completed jobs can be retried." });

        job.Status = ScrapeJobStatus.Queued;
        job.Error = null;
        job.StartedAt = null;
        job.CompletedAt = null;
        job.RecordsProcessed = 0;
        job.RecordsFailed = 0;
        await _db.SaveChangesAsync(ct);

        queue.TryEnqueue(job.Id);

        return AcceptedAtAction(nameof(GetJob), new { id = job.Id }, job.ToDto());
    }
}
