using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebScraper.Api.Auth;
using WebScraper.Api.Dtos;
using WebScraper.Api.Mapping;
using WebScraper.Api.Pagination;
using WebScraper.Data;

namespace WebScraper.Api.Controllers;

/// <summary>
/// Read-only endpoints for NFL teams. All results are sourced from the local
/// database — see the scraper CLI (or M2 SignalR refresh jobs) for ingestion.
/// </summary>
[ApiController]
[Route("api/v1/teams")]
[Authorize(Policy = AuthorizationPolicies.RequireReadScope)]
[Produces("application/json")]
public class TeamsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TeamsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>List teams, optionally filtered by conference (AFC/NFC).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TeamDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TeamDto>>> GetTeams(
        [FromQuery] string? conference,
        [FromQuery] PaginationQuery pagination,
        CancellationToken cancellationToken)
    {
        var query = _db.Teams.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(conference))
        {
            query = query.Where(t => t.Conference == conference);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(t => t.Abbreviation)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        Response.Headers["X-Total-Count"] = totalCount.ToString();

        var dtos = items.Select(t => t.ToDto()).ToList();
        return Ok(PagedResult<TeamDto>.From(dtos, pagination.Page, pagination.PageSize, totalCount));
    }

    /// <summary>Fetch a single team by its primary key.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> GetTeamById(int id, CancellationToken cancellationToken)
    {
        var team = await _db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (team is null)
        {
            return Problem(
                title: "Team not found",
                detail: $"No team exists with id {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }
        return Ok(team.ToDto());
    }

    /// <summary>Fetch a single team by its NFL abbreviation (e.g. KC, SF).</summary>
    [HttpGet("by-abbreviation/{abbreviation}")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> GetTeamByAbbreviation(string abbreviation, CancellationToken cancellationToken)
    {
        var team = await _db.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Abbreviation == abbreviation, cancellationToken);
        if (team is null)
        {
            return Problem(
                title: "Team not found",
                detail: $"No team exists with abbreviation '{abbreviation}'.",
                statusCode: StatusCodes.Status404NotFound);
        }
        return Ok(team.ToDto());
    }
}
