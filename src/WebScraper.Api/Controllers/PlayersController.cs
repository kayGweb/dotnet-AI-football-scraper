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
/// Read-only endpoints for NFL players. Eager-loads the team nav property so
/// <see cref="PlayerDto.TeamAbbreviation"/> can be populated without N+1 queries.
/// </summary>
[ApiController]
[Route("api/v1/players")]
[Authorize(Policy = AuthorizationPolicies.RequireReadScope)]
[Produces("application/json")]
public class PlayersController : ControllerBase
{
    private readonly AppDbContext _db;

    public PlayersController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// List players with optional filters for team and position. Supply either
    /// <paramref name="teamId"/> or <paramref name="teamAbbreviation"/>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PlayerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PlayerDto>>> GetPlayers(
        [FromQuery] int? teamId,
        [FromQuery] string? teamAbbreviation,
        [FromQuery] string? position,
        [FromQuery] PaginationQuery pagination,
        CancellationToken cancellationToken)
    {
        var query = _db.Players
            .AsNoTracking()
            .Include(p => p.Team)
            .AsQueryable();

        if (teamId.HasValue)
        {
            query = query.Where(p => p.TeamId == teamId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(teamAbbreviation))
        {
            query = query.Where(p => p.Team != null && p.Team.Abbreviation == teamAbbreviation);
        }

        if (!string.IsNullOrWhiteSpace(position))
        {
            query = query.Where(p => p.Position == position);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        Response.Headers["X-Total-Count"] = totalCount.ToString();

        var dtos = items.Select(p => p.ToDto()).ToList();
        return Ok(PagedResult<PlayerDto>.From(dtos, pagination.Page, pagination.PageSize, totalCount));
    }

    /// <summary>Fetch a single player by primary key.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PlayerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerDto>> GetPlayerById(int id, CancellationToken cancellationToken)
    {
        var player = await _db.Players
            .AsNoTracking()
            .Include(p => p.Team)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (player is null)
        {
            return Problem(
                title: "Player not found",
                detail: $"No player exists with id {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }
        return Ok(player.ToDto());
    }

    /// <summary>
    /// Fetch a player's per-game stats for a given season (optionally filtered
    /// to a single week).
    /// </summary>
    [HttpGet("{id:int}/stats")]
    [ProducesResponseType(typeof(IReadOnlyList<PlayerGameStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PlayerGameStatsDto>>> GetPlayerStats(
        int id,
        [FromQuery] int? season,
        [FromQuery] int? week,
        CancellationToken cancellationToken)
    {
        var playerExists = await _db.Players.AnyAsync(p => p.Id == id, cancellationToken);
        if (!playerExists)
        {
            return Problem(
                title: "Player not found",
                detail: $"No player exists with id {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var query = _db.PlayerGameStats
            .AsNoTracking()
            .Include(s => s.Player)
            .Include(s => s.Game)
            .Where(s => s.PlayerId == id);

        if (season.HasValue)
        {
            query = query.Where(s => s.Game != null && s.Game.Season == season.Value);
        }
        if (week.HasValue)
        {
            query = query.Where(s => s.Game != null && s.Game.Week == week.Value);
        }

        var items = await query
            .OrderBy(s => s.Game!.Season)
            .ThenBy(s => s.Game!.Week)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(s => s.ToDto()).ToList());
    }
}
