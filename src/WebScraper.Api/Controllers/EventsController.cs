using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebScraper.Api.Auth;
using WebScraper.Api.Dtos.Admin;
using WebScraper.Data;

namespace WebScraper.Api.Controllers;

/// <summary>
/// Replay endpoint for ScrapeEvents. Clients that subscribe to the SignalR
/// hub track the last-seen event Id and call this endpoint after reconnects
/// to catch up on missed events.
/// </summary>
[ApiController]
[Route("api/v1/events")]
[Authorize(Policy = AuthorizationPolicies.RequireViewer)]
[Produces("application/json")]
public class EventsController : ControllerBase
{
    private const int DefaultTake = 100;
    private const int MaxTake = 500;

    private readonly AppDbContext _db;

    public EventsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Returns scrape events with Id greater than <paramref name="since"/>, ordered ascending.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ScrapeEventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ScrapeEventDto>>> GetEvents(
        [FromQuery] long since = 0,
        [FromQuery] int take = DefaultTake)
    {
        if (take < 1) take = DefaultTake;
        if (take > MaxTake) take = MaxTake;

        var events = await _db.ScrapeEvents
            .AsNoTracking()
            .Where(e => e.Id > since)
            .OrderBy(e => e.Id)
            .Take(take)
            .ToListAsync();

        return Ok(events.Select(e => e.ToDto()));
    }
}
