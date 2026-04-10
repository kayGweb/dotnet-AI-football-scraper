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
/// Read-only endpoints for NFL stadiums/venues. Venues are upserted by ESPN
/// venue id during scrape runs.
/// </summary>
[ApiController]
[Route("api/v1/venues")]
[Authorize(Policy = AuthorizationPolicies.RequireReadScope)]
[Produces("application/json")]
public class VenuesController : ControllerBase
{
    private readonly AppDbContext _db;

    public VenuesController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>List venues, optionally filtered by state or indoor/outdoor.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<VenueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<VenueDto>>> GetVenues(
        [FromQuery] string? state,
        [FromQuery] bool? isIndoor,
        [FromQuery] PaginationQuery pagination,
        CancellationToken cancellationToken)
    {
        var query = _db.Venues.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(state))
        {
            query = query.Where(v => v.State == state);
        }
        if (isIndoor.HasValue)
        {
            query = query.Where(v => v.IsIndoor == isIndoor.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(v => v.Name)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        Response.Headers["X-Total-Count"] = totalCount.ToString();

        var dtos = items.Select(v => v.ToDto()).ToList();
        return Ok(PagedResult<VenueDto>.From(dtos, pagination.Page, pagination.PageSize, totalCount));
    }

    /// <summary>Fetch a single venue by primary key.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(VenueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VenueDto>> GetVenueById(int id, CancellationToken cancellationToken)
    {
        var venue = await _db.Venues.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (venue is null)
        {
            return Problem(
                title: "Venue not found",
                detail: $"No venue exists with id {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }
        return Ok(venue.ToDto());
    }
}
