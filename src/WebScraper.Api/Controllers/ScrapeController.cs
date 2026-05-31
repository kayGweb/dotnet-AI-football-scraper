using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WebScraper.Api.Auth;
using WebScraper.Api.Dtos.Admin;
using WebScraper.Api.Services;
using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Api.Controllers;

/// <summary>
/// Triggers scrape jobs. Each POST creates a persisted ScrapeJob row, enqueues
/// it, and returns 202 Accepted with the job ID so the caller can poll status.
/// </summary>
[ApiController]
[Route("api/v1/scrape")]
[Authorize(Policy = AuthorizationPolicies.RequireOperator)]
[Produces("application/json")]
public class ScrapeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IJobQueue _queue;
    private readonly ScraperSettings _scraperSettings;

    public ScrapeController(
        AppDbContext db,
        IJobQueue queue,
        IOptions<ScraperSettings> scraperSettings)
    {
        _db = db;
        _queue = queue;
        _scraperSettings = scraperSettings.Value;
    }

    /// <summary>Scrape all NFL teams.</summary>
    [HttpPost("teams")]
    [ProducesResponseType(typeof(ScrapeJobDto), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ScrapeTeams()
    {
        return await EnqueueJob(ScrapeJobType.Teams);
    }

    /// <summary>Scrape player rosters for all teams.</summary>
    [HttpPost("players")]
    [ProducesResponseType(typeof(ScrapeJobDto), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ScrapePlayers()
    {
        return await EnqueueJob(ScrapeJobType.Players);
    }

    /// <summary>Scrape games for a season (optionally a specific week).</summary>
    [HttpPost("games")]
    [ProducesResponseType(typeof(ScrapeJobDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ScrapeGames([FromBody] CreateScrapeJobRequest request)
    {
        if (request.Season is null)
        {
            return Problem(
                title: "Season is required",
                detail: "Provide a season (e.g. 2025) in the request body.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return await EnqueueJob(ScrapeJobType.Games, request.Season, request.Week);
    }

    /// <summary>Scrape player stats for a specific season and week.</summary>
    [HttpPost("stats")]
    [ProducesResponseType(typeof(ScrapeJobDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ScrapeStats([FromBody] CreateScrapeJobRequest request)
    {
        if (request.Season is null || request.Week is null)
        {
            return Problem(
                title: "Season and week are required",
                detail: "Provide both season and week (e.g. season=2025, week=1) in the request body.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return await EnqueueJob(ScrapeJobType.Stats, request.Season, request.Week);
    }

    /// <summary>Run the full scrape pipeline (teams, players, games, optionally stats).</summary>
    [HttpPost("all")]
    [ProducesResponseType(typeof(ScrapeJobDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ScrapeAll([FromBody] CreateScrapeJobRequest request)
    {
        if (request.Season is null)
        {
            return Problem(
                title: "Season is required",
                detail: "Provide a season (e.g. 2025) in the request body.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return await EnqueueJob(ScrapeJobType.All, request.Season, request.Week);
    }

    private async Task<IActionResult> EnqueueJob(ScrapeJobType type, int? season = null, int? week = null)
    {
        var job = new ScrapeJob
        {
            Type = type,
            Source = _scraperSettings.DataProvider,
            Season = season,
            Week = week,
            Status = ScrapeJobStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            RequestedBy = User.FindFirstValue(ClaimTypes.Email),
        };

        _db.ScrapeJobs.Add(job);
        await _db.SaveChangesAsync();

        _queue.TryEnqueue(job.Id);

        return AcceptedAtAction(
            actionName: nameof(JobsController.GetJob),
            controllerName: "Jobs",
            routeValues: new { id = job.Id },
            value: job.ToDto());
    }
}
