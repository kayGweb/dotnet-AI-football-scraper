using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebScraper.Api.Auth;
using WebScraper.Api.Dtos.Admin;
using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Api.Controllers;

/// <summary>
/// Admin review of soft-deleted rows across every domain entity. Restore is per-row;
/// bulk restore is deliberately omitted to force a deliberate per-item decision.
/// </summary>
[ApiController]
[Route("api/v1/deleted-items")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
[Produces("application/json")]
public class DeletedItemsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeletedItemsController> _logger;

    public DeletedItemsController(AppDbContext db, ILogger<DeletedItemsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// List all soft-deleted rows, newest first. Optional <c>entityType</c> filter narrows
    /// to one entity (case-insensitive — e.g. "team", "Player", "APILINK").
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DeletedItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DeletedItemDto>>> List(
        [FromQuery] string? entityType,
        CancellationToken ct)
    {
        var results = new List<DeletedItemDto>();
        var filter = entityType?.Trim().ToLowerInvariant();

        if (filter is null or "team")
            results.AddRange(await _db.Teams.IgnoreQueryFilters().AsNoTracking()
                .Where(t => t.IsDeleted)
                .Select(t => new DeletedItemDto
                {
                    EntityType = "Team",
                    Id = t.Id,
                    Label = $"{t.Abbreviation} — {t.City} {t.Name}",
                    DeletedAt = t.DeletedAt!.Value,
                    DeletedBy = t.DeletedBy,
                    DeleteReason = t.DeleteReason,
                })
                .ToListAsync(ct));

        if (filter is null or "player")
            results.AddRange(await _db.Players.IgnoreQueryFilters().AsNoTracking()
                .Where(p => p.IsDeleted)
                .Select(p => new DeletedItemDto
                {
                    EntityType = "Player",
                    Id = p.Id,
                    Label = p.Name,
                    DeletedAt = p.DeletedAt!.Value,
                    DeletedBy = p.DeletedBy,
                    DeleteReason = p.DeleteReason,
                })
                .ToListAsync(ct));

        if (filter is null or "game")
            results.AddRange(await _db.Games.IgnoreQueryFilters().AsNoTracking()
                .Where(g => g.IsDeleted)
                .Select(g => new DeletedItemDto
                {
                    EntityType = "Game",
                    Id = g.Id,
                    Label = $"Season {g.Season} W{g.Week} ({g.AwayTeamId} @ {g.HomeTeamId})",
                    DeletedAt = g.DeletedAt!.Value,
                    DeletedBy = g.DeletedBy,
                    DeleteReason = g.DeleteReason,
                })
                .ToListAsync(ct));

        if (filter is null or "playergamestats" or "stats")
            results.AddRange(await _db.PlayerGameStats.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.IsDeleted)
                .Select(s => new DeletedItemDto
                {
                    EntityType = "PlayerGameStats",
                    Id = s.Id,
                    Label = $"Player {s.PlayerId} / Game {s.GameId}",
                    DeletedAt = s.DeletedAt!.Value,
                    DeletedBy = s.DeletedBy,
                    DeleteReason = s.DeleteReason,
                })
                .ToListAsync(ct));

        if (filter is null or "venue")
            results.AddRange(await _db.Venues.IgnoreQueryFilters().AsNoTracking()
                .Where(v => v.IsDeleted)
                .Select(v => new DeletedItemDto
                {
                    EntityType = "Venue",
                    Id = v.Id,
                    Label = $"{v.Name} ({v.City})",
                    DeletedAt = v.DeletedAt!.Value,
                    DeletedBy = v.DeletedBy,
                    DeleteReason = v.DeleteReason,
                })
                .ToListAsync(ct));

        if (filter is null or "teamgamestats")
            results.AddRange(await _db.TeamGameStats.IgnoreQueryFilters().AsNoTracking()
                .Where(t => t.IsDeleted)
                .Select(t => new DeletedItemDto
                {
                    EntityType = "TeamGameStats",
                    Id = t.Id,
                    Label = $"Game {t.GameId} / Team {t.TeamId}",
                    DeletedAt = t.DeletedAt!.Value,
                    DeletedBy = t.DeletedBy,
                    DeleteReason = t.DeleteReason,
                })
                .ToListAsync(ct));

        if (filter is null or "injury")
            results.AddRange(await _db.Injuries.IgnoreQueryFilters().AsNoTracking()
                .Where(i => i.IsDeleted)
                .Select(i => new DeletedItemDto
                {
                    EntityType = "Injury",
                    Id = i.Id,
                    Label = $"{i.PlayerName} (Game {i.GameId})",
                    DeletedAt = i.DeletedAt!.Value,
                    DeletedBy = i.DeletedBy,
                    DeleteReason = i.DeleteReason,
                })
                .ToListAsync(ct));

        if (filter is null or "apilink")
            results.AddRange(await _db.ApiLinks.IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.IsDeleted)
                .Select(a => new DeletedItemDto
                {
                    EntityType = "ApiLink",
                    Id = a.Id,
                    Label = a.Url,
                    DeletedAt = a.DeletedAt!.Value,
                    DeletedBy = a.DeletedBy,
                    DeleteReason = a.DeleteReason,
                })
                .ToListAsync(ct));

        if (filter is null or "apikey")
            results.AddRange(await _db.ApiKeys.IgnoreQueryFilters().AsNoTracking()
                .Where(k => k.IsDeleted)
                .Select(k => new DeletedItemDto
                {
                    EntityType = "ApiKey",
                    Id = k.Id,
                    Label = $"{k.KeyId} ({k.Name})",
                    DeletedAt = k.DeletedAt!.Value,
                    DeletedBy = k.DeletedBy,
                    DeleteReason = k.DeleteReason,
                })
                .ToListAsync(ct));

        return Ok(results.OrderByDescending(r => r.DeletedAt));
    }

    /// <summary>
    /// Restore a soft-deleted row. Sets <c>IsDeleted=false</c> and clears deletion metadata —
    /// the row reappears in normal queries immediately. Returns 404 if the row doesn't exist
    /// or isn't currently deleted.
    /// </summary>
    [HttpPost("{entityType}/{id:int}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Restore(string entityType, int id, CancellationToken ct)
    {
        var restoredBy = User.Identity?.Name ?? "unknown";
        var key = entityType.Trim().ToLowerInvariant();

        var ok = key switch
        {
            "team" => await ClearDeleteAsync(_db.Teams.IgnoreQueryFilters(), id, ct),
            "player" => await ClearDeleteAsync(_db.Players.IgnoreQueryFilters(), id, ct),
            "game" => await ClearDeleteAsync(_db.Games.IgnoreQueryFilters(), id, ct),
            "playergamestats" or "stats" => await ClearDeleteAsync(_db.PlayerGameStats.IgnoreQueryFilters(), id, ct),
            "venue" => await ClearDeleteAsync(_db.Venues.IgnoreQueryFilters(), id, ct),
            "teamgamestats" => await ClearDeleteAsync(_db.TeamGameStats.IgnoreQueryFilters(), id, ct),
            "injury" => await ClearDeleteAsync(_db.Injuries.IgnoreQueryFilters(), id, ct),
            "apilink" => await ClearDeleteAsync(_db.ApiLinks.IgnoreQueryFilters(), id, ct),
            "apikey" => await ClearDeleteAsync(_db.ApiKeys.IgnoreQueryFilters(), id, ct),
            _ => (bool?)null,
        };

        if (ok is null)
        {
            return Problem(
                title: "Unknown entity type",
                detail: $"'{entityType}' is not a recognised soft-deletable entity.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        if (ok == false)
        {
            return Problem(
                title: "Not found or not deleted",
                detail: $"No soft-deleted {entityType} exists with id {id}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        _logger.LogInformation("Restored {EntityType} {Id} by {User}", entityType, id, restoredBy);
        return NoContent();
    }

    // Hardcodes restore semantics: clear IsDeleted/DeletedAt/DeletedBy/DeleteReason in one
    // round trip. Uses ExecuteUpdateAsync to avoid round-tripping the entity and to bypass
    // the interceptor's UpdatedAt stamp (restoring shouldn't lie about the data freshness).
    private static async Task<bool> ClearDeleteAsync<T>(IQueryable<T> source, int id, CancellationToken ct)
        where T : class, ISoftDeletable
    {
        // EF Core 8 ExecuteUpdateAsync — no entity materialisation.
        var affected = await source
            .Where(e => EF.Property<int>(e, "Id") == id && e.IsDeleted)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.IsDeleted, false)
                .SetProperty(e => e.DeletedAt, (DateTime?)null)
                .SetProperty(e => e.DeletedBy, (string?)null)
                .SetProperty(e => e.DeleteReason, (string?)null),
                ct);
        return affected > 0;
    }
}
