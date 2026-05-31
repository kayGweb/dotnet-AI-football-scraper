using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebScraper.Api.Auth;
using WebScraper.Api.Dtos;
using WebScraper.Data;

namespace WebScraper.Api.Controllers;

/// <summary>
/// Lightweight database snapshot used by Claude, dashboards, and operators to
/// confirm the scraper pipeline is populating tables. Separate from the
/// infrastructure <c>/health</c> endpoint — this one hits the domain tables.
/// </summary>
[ApiController]
[Route("api/v1/status")]
[Authorize(Policy = AuthorizationPolicies.RequireReadScope)]
[Produces("application/json")]
public class StatusController : ControllerBase
{
    private readonly AppDbContext _db;

    public StatusController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Return record counts for every domain table plus the most recent update timestamp.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(StatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var dto = new StatusDto
        {
            Teams = await _db.Teams.CountAsync(cancellationToken),
            Players = await _db.Players.CountAsync(cancellationToken),
            Games = await _db.Games.CountAsync(cancellationToken),
            PlayerGameStats = await _db.PlayerGameStats.CountAsync(cancellationToken),
            Venues = await _db.Venues.CountAsync(cancellationToken),
            TeamGameStats = await _db.TeamGameStats.CountAsync(cancellationToken),
            Injuries = await _db.Injuries.CountAsync(cancellationToken),
            ApiLinks = await _db.ApiLinks.CountAsync(cancellationToken),
        };

        // Latest UpdatedAt across the main entities. Used as a freshness heartbeat.
        var latestTeam = await _db.Teams.MaxAsync(t => (DateTime?)t.UpdatedAt, cancellationToken);
        var latestPlayer = await _db.Players.MaxAsync(p => (DateTime?)p.UpdatedAt, cancellationToken);
        var latestGame = await _db.Games.MaxAsync(g => (DateTime?)g.UpdatedAt, cancellationToken);
        var latestStats = await _db.PlayerGameStats.MaxAsync(s => (DateTime?)s.UpdatedAt, cancellationToken);

        dto.LatestUpdate = new[] { latestTeam, latestPlayer, latestGame, latestStats }
            .Where(d => d.HasValue)
            .Max();

        return Ok(dto);
    }
}
