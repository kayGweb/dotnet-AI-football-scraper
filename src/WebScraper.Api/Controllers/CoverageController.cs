using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebScraper.Api.Auth;
using WebScraper.Api.Dtos.Admin;
using WebScraper.Api.Services;
using WebScraper.Models;
using WebScraper.Services.Coverage;

namespace WebScraper.Api.Controllers;

[ApiController]
[Route("api/v1/coverage")]
[Authorize(Policy = AuthorizationPolicies.RequireOperateScope)]
[Produces("application/json")]
public class CoverageController : ControllerBase
{
    private readonly SeasonCoverageService _coverage;
    private readonly QualityRulesEngine _quality;
    private readonly RepairJobEnqueuer _repair;
    private readonly IJobQueue _queue;

    public CoverageController(
        SeasonCoverageService coverage,
        QualityRulesEngine quality,
        RepairJobEnqueuer repair,
        IJobQueue queue)
    {
        _coverage = coverage;
        _quality = quality;
        _repair = repair;
        _queue = queue;
    }

    /// <summary>Get coverage snapshots (optionally filtered by season/seasonType).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SeasonCoverageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SeasonCoverageDto>>> GetCoverage(
        [FromQuery] int? season,
        [FromQuery] NflSeasonType? seasonType,
        CancellationToken cancellationToken)
    {
        var rows = await _coverage.GetAsync(season, seasonType, cancellationToken);
        return Ok(rows.Select(ToDto));
    }

    /// <summary>Recompute coverage, run quality rules, and optionally enqueue repairs.</summary>
    [HttpPost("refresh")]
    [Authorize(Policy = AuthorizationPolicies.RequireOperate)]
    [ProducesResponseType(typeof(CoverageRefreshResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CoverageRefreshResultDto>> Refresh(
        [FromQuery] int? season,
        [FromQuery] NflSeasonType? seasonType,
        [FromQuery] bool enqueueRepairs = false,
        CancellationToken cancellationToken = default)
    {
        var refreshed = await _coverage.RefreshAsync(season, seasonType, cancellationToken);
        var findings = await _quality.RunAsync(season, seasonType, week: null, cancellationToken);

        var repairsEnqueued = 0;
        if (enqueueRepairs)
        {
            var jobIds = await _repair.EnqueueRepairsForOpenFindingsAsync(50, cancellationToken: cancellationToken);
            foreach (var id in jobIds)
                _queue.TryEnqueue(id);
            repairsEnqueued = jobIds.Count;
        }

        var openCount = findings.Count;

        return Ok(new CoverageRefreshResultDto
        {
            WeeksRefreshed = refreshed.Count,
            OpenFindings = openCount,
            RepairsEnqueued = repairsEnqueued,
        });
    }

    private static SeasonCoverageDto ToDto(SeasonCoverage row)
    {
        var status = ComputeStatus(row);
        return new SeasonCoverageDto
        {
            Season = row.Season,
            SeasonType = row.SeasonType,
            Week = row.Week,
            ExpectedGames = row.ExpectedGames,
            ActualGames = row.ActualGames,
            GamesWithPlayerStats = row.GamesWithPlayerStats,
            GamesWithTeamStats = row.GamesWithTeamStats,
            GamesWithInjuries = row.GamesWithInjuries,
            GamesWithOdds = row.GamesWithOdds,
            PlayerCount = row.PlayerCount,
            LastVerifiedAt = row.LastVerifiedAt,
            Status = status,
        };
    }

    private static string ComputeStatus(SeasonCoverage row)
    {
        if (row.ActualGames == 0)
            return row.ExpectedGames > 0 ? "missing" : "empty";

        if (row.ExpectedGames is int expected && row.ActualGames < expected)
            return "partial";

        if (row.ActualGames > 0 && row.GamesWithPlayerStats < row.ActualGames)
            return "no-stats";

        if (row.ExpectedGames is int exp && row.ActualGames >= exp &&
            row.GamesWithPlayerStats >= row.ActualGames)
            return "complete";

        return row.GamesWithPlayerStats >= row.ActualGames ? "complete" : "partial";
    }
}
