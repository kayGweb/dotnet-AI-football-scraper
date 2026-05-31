using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebScraper.Api.Auth;
using WebScraper.Data;
using WebScraper.Models;
using WebScraper.Services;

namespace WebScraper.Api.Controllers;

/// <summary>
/// Pushes the local DB to a remote PostgreSQL — the same operation as the CLI's
/// <c>push</c> command, exposed as an HTTP endpoint so admins can trigger it from
/// the dashboard. The connection string is read from <c>ConnectionStrings:PostgreSQL</c>
/// (set in appsettings.Local.json or via env var).
///
/// Synchronous for now — pushes are tens of seconds, not minutes. When that becomes
/// painful, M3 chunk (b) turns this into a job-queue trigger that returns 202 + jobId.
/// </summary>
[ApiController]
[Route("api/v1/push")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
[Produces("application/json")]
public class PushController : ControllerBase
{
    private readonly AppDbContext _localDb;
    private readonly DatabasePushService _pushService;
    private readonly ConsoleDisplayService _display;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PushController> _logger;

    public PushController(
        AppDbContext localDb,
        DatabasePushService pushService,
        ConsoleDisplayService display,
        IConfiguration configuration,
        ILogger<PushController> logger)
    {
        _localDb = localDb;
        _pushService = pushService;
        _display = display;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Push all local SQLite data to the remote PostgreSQL configured in <c>ConnectionStrings:PostgreSQL</c>.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ScrapeResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScrapeResult), StatusCodes.Status207MultiStatus)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ScrapeResult>> Push()
    {
        var connectionString = _configuration.GetConnectionString("PostgreSQL");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Problem(
                title: "PostgreSQL connection not configured",
                detail: "Set ConnectionStrings:PostgreSQL in appsettings.Local.json or the DATABASE_URL env var.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var requestedBy = User.Identity?.Name ?? "unknown";
        _logger.LogInformation("Push triggered by {User}", requestedBy);

        var result = await _pushService.PushToServerAsync(_localDb, connectionString, _display);

        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, result);
        }
        if (result.Errors.Count > 0)
        {
            return StatusCode(StatusCodes.Status207MultiStatus, result);
        }
        return Ok(result);
    }
}
