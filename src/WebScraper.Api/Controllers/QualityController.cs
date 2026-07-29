using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebScraper.Api.Auth;
using WebScraper.Api.Dtos.Admin;
using WebScraper.Api.Services;
using WebScraper.Services.Coverage;

namespace WebScraper.Api.Controllers;

[ApiController]
[Route("api/v1/quality")]
[Authorize(Policy = AuthorizationPolicies.RequireViewer)]
[Produces("application/json")]
public class QualityController : ControllerBase
{
    private readonly QualityRulesEngine _quality;
    private readonly RepairJobEnqueuer _repair;
    private readonly IJobQueue _queue;

    public QualityController(
        QualityRulesEngine quality,
        RepairJobEnqueuer repair,
        IJobQueue queue)
    {
        _quality = quality;
        _repair = repair;
        _queue = queue;
    }

    /// <summary>List open data quality findings.</summary>
    [HttpGet("findings")]
    [ProducesResponseType(typeof(IEnumerable<DataQualityFindingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DataQualityFindingDto>>> GetFindings(
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var findings = await _quality.GetOpenFindingsAsync(limit, cancellationToken);
        return Ok(findings.Select(f => new DataQualityFindingDto
        {
            Id = f.Id,
            RuleType = f.RuleType.ToString(),
            Severity = f.Severity.ToString(),
            Status = f.Status.ToString(),
            EntityType = f.EntityType,
            EntityId = f.EntityId,
            Season = f.Season,
            SeasonType = f.SeasonType,
            Week = f.Week,
            Message = f.Message,
            RepairJobId = f.RepairJobId,
            CreatedAt = f.CreatedAt,
        }));
    }

    /// <summary>Run quality rules scan.</summary>
    [HttpPost("scan")]
    [Authorize(Policy = AuthorizationPolicies.RequireOperator)]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> Scan(
        [FromQuery] int? season,
        [FromQuery] WebScraper.Models.NflSeasonType? seasonType,
        [FromQuery] int? week,
        CancellationToken cancellationToken = default)
    {
        var findings = await _quality.RunAsync(season, seasonType, week, cancellationToken);
        return Ok(findings.Count);
    }

    /// <summary>Enqueue a repair scrape job for one finding.</summary>
    [HttpPost("findings/{id:long}/repair")]
    [Authorize(Policy = AuthorizationPolicies.RequireOperator)]
    [ProducesResponseType(typeof(int), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RepairFinding(long id, CancellationToken cancellationToken)
    {
        var jobId = await _repair.EnqueueRepairForFindingAsync(id, cancellationToken: cancellationToken);
        if (jobId is null)
            return NotFound();

        _queue.TryEnqueue(jobId.Value);
        return Accepted(jobId.Value);
    }

    /// <summary>Enqueue repair jobs for all actionable open findings.</summary>
    [HttpPost("repairs")]
    [Authorize(Policy = AuthorizationPolicies.RequireOperator)]
    [ProducesResponseType(typeof(int), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> EnqueueRepairs(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var jobIds = await _repair.EnqueueRepairsForOpenFindingsAsync(limit, cancellationToken: cancellationToken);
        foreach (var jobId in jobIds)
            _queue.TryEnqueue(jobId);

        return Accepted(jobIds.Count);
    }
}
