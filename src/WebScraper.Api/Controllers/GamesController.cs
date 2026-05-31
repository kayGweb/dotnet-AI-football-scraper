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
/// Read-only endpoints for games plus related per-game stats (team + player)
/// and injuries. Eager-loads the team/venue nav properties so list rows can be
/// rendered in one query.
/// </summary>
[ApiController]
[Route("api/v1/games")]
[Authorize(Policy = AuthorizationPolicies.RequireReadScope)]
[Produces("application/json")]
public class GamesController : ControllerBase
{
    private readonly AppDbContext _db;

    public GamesController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>List games filtered by season, week, or team.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<GameDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<GameDto>>> GetGames(
        [FromQuery] int? season,
        [FromQuery] int? week,
        [FromQuery] int? teamId,
        [FromQuery] PaginationQuery pagination,
        CancellationToken cancellationToken)
    {
        var query = _db.Games
            .AsNoTracking()
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Include(g => g.Venue)
            .AsQueryable();

        if (season.HasValue)
        {
            query = query.Where(g => g.Season == season.Value);
        }
        if (week.HasValue)
        {
            query = query.Where(g => g.Week == week.Value);
        }
        if (teamId.HasValue)
        {
            query = query.Where(g => g.HomeTeamId == teamId.Value || g.AwayTeamId == teamId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(g => g.Season)
            .ThenBy(g => g.Week)
            .ThenBy(g => g.GameDate)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        Response.Headers["X-Total-Count"] = totalCount.ToString();

        var dtos = items.Select(g => g.ToDto()).ToList();
        return Ok(PagedResult<GameDto>.From(dtos, pagination.Page, pagination.PageSize, totalCount));
    }

    /// <summary>Fetch a single game by primary key, including venue and teams.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(GameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameDto>> GetGameById(int id, CancellationToken cancellationToken)
    {
        var game = await _db.Games
            .AsNoTracking()
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Include(g => g.Venue)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (game is null)
        {
            return Problem(
                title: "Game not found",
                detail: $"No game exists with id {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }
        return Ok(game.ToDto());
    }

    /// <summary>List team-level per-game stats for a given game (home + away).</summary>
    [HttpGet("{id:int}/team-stats")]
    [ProducesResponseType(typeof(IReadOnlyList<TeamGameStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<TeamGameStatsDto>>> GetTeamStatsForGame(int id, CancellationToken cancellationToken)
    {
        var gameExists = await _db.Games.AnyAsync(g => g.Id == id, cancellationToken);
        if (!gameExists)
        {
            return Problem(
                title: "Game not found",
                detail: $"No game exists with id {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var rows = await _db.TeamGameStats
            .AsNoTracking()
            .Include(s => s.Team)
            .Where(s => s.GameId == id)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(s => s.ToDto()).ToList());
    }

    /// <summary>List all player-level per-game stats for a given game.</summary>
    [HttpGet("{id:int}/player-stats")]
    [ProducesResponseType(typeof(IReadOnlyList<PlayerGameStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<PlayerGameStatsDto>>> GetPlayerStatsForGame(int id, CancellationToken cancellationToken)
    {
        var gameExists = await _db.Games.AnyAsync(g => g.Id == id, cancellationToken);
        if (!gameExists)
        {
            return Problem(
                title: "Game not found",
                detail: $"No game exists with id {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var rows = await _db.PlayerGameStats
            .AsNoTracking()
            .Include(s => s.Player)
            .Include(s => s.Game)
            .Where(s => s.GameId == id)
            .OrderBy(s => s.Player!.Name)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(s => s.ToDto()).ToList());
    }

    /// <summary>List injuries reported for a given game.</summary>
    [HttpGet("{id:int}/injuries")]
    [ProducesResponseType(typeof(IReadOnlyList<InjuryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<InjuryDto>>> GetInjuriesForGame(int id, CancellationToken cancellationToken)
    {
        var gameExists = await _db.Games.AnyAsync(g => g.Id == id, cancellationToken);
        if (!gameExists)
        {
            return Problem(
                title: "Game not found",
                detail: $"No game exists with id {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var rows = await _db.Injuries
            .AsNoTracking()
            .Where(i => i.GameId == id)
            .OrderBy(i => i.PlayerName)
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(i => i.ToDto()).ToList());
    }
}
