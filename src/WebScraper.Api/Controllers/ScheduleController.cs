using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebScraper.Api.Auth;
using WebScraper.Api.Dtos;
using WebScraper.Api.Mapping;
using WebScraper.Data;
using WebScraper.Models;
using WebScraper.Services.Coverage;

namespace WebScraper.Api.Controllers;

/// <summary>
/// Read-only schedule views over the Games table, ordered by kickoff. Unlike
/// <c>/api/v1/games</c> these are unpaged (a full season is at most ~285 games) and
/// default to the current NFL season, so a caller can ask "what's the schedule?"
/// without knowing the season year. Scheduled games have null scores.
/// </summary>
[ApiController]
[Route("api/v1/schedule")]
[Authorize(Policy = AuthorizationPolicies.RequireReadScope)]
[Produces("application/json")]
public class ScheduleController : ControllerBase
{
    private const int MaxUpcomingDays = 60;

    private readonly AppDbContext _db;

    public ScheduleController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Full schedule for a season, ordered by kickoff. Defaults to the current NFL
    /// season and the regular season.
    /// </summary>
    /// <param name="season">Season year (e.g. 2026). Defaults to the current NFL season.</param>
    /// <param name="seasonType">Preseason, Regular (default), or Postseason.</param>
    /// <param name="week">Optional week filter.</param>
    /// <param name="team">Optional NFL team abbreviation (e.g. KC) — matches home or away.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GameDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GameDto>>> GetSchedule(
        [FromQuery] int? season,
        [FromQuery] NflSeasonType seasonType = NflSeasonType.Regular,
        [FromQuery] int? week = null,
        [FromQuery] string? team = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedSeason = season ?? NflSeasonSchedule.GetCurrentSeason(DateTime.UtcNow);

        var query = ScheduleQuery()
            .Where(g => g.Season == resolvedSeason && g.SeasonType == seasonType);

        if (week.HasValue)
            query = query.Where(g => g.Week == week.Value);

        query = FilterByTeam(query, team);

        var games = await query
            .OrderBy(g => g.GameDate)
            .ThenBy(g => g.Id)
            .ToListAsync(cancellationToken);

        Response.Headers["X-Total-Count"] = games.Count.ToString();
        return Ok(games.Select(g => g.ToDto()).ToList());
    }

    /// <summary>
    /// Games kicking off within the next <paramref name="days"/> days, across all
    /// season types. Includes games that kicked off in the last few hours so live
    /// games still appear.
    /// </summary>
    /// <param name="days">Look-ahead window in days, 1–60. Defaults to 7.</param>
    /// <param name="team">Optional NFL team abbreviation (e.g. KC) — matches home or away.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet("upcoming")]
    [ProducesResponseType(typeof(IReadOnlyList<GameDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GameDto>>> GetUpcoming(
        [FromQuery] int days = 7,
        [FromQuery] string? team = null,
        CancellationToken cancellationToken = default)
    {
        var window = Math.Clamp(days, 1, MaxUpcomingDays);
        var now = DateTime.UtcNow;
        var from = now.AddHours(-4);
        var to = now.AddDays(window);

        var query = ScheduleQuery()
            .Where(g => g.GameDate >= from && g.GameDate <= to);

        query = FilterByTeam(query, team);

        var games = await query
            .OrderBy(g => g.GameDate)
            .ThenBy(g => g.Id)
            .ToListAsync(cancellationToken);

        Response.Headers["X-Total-Count"] = games.Count.ToString();
        return Ok(games.Select(g => g.ToDto()).ToList());
    }

    private IQueryable<Game> ScheduleQuery() => _db.Games
        .AsNoTracking()
        .Include(g => g.HomeTeamSeason)
        .Include(g => g.AwayTeamSeason)
        .Include(g => g.Venue);

    private static IQueryable<Game> FilterByTeam(IQueryable<Game> query, string? team)
    {
        if (string.IsNullOrWhiteSpace(team))
            return query;

        var abbr = team.Trim().ToUpperInvariant();
        return query.Where(g =>
            g.HomeTeamSeason.Abbreviation.ToUpper() == abbr
            || g.AwayTeamSeason.Abbreviation.ToUpper() == abbr);
    }
}
