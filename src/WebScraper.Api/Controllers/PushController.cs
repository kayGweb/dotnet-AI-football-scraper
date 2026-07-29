using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebScraper.Api.Auth;
using WebScraper.Api.Dtos.Admin;
using WebScraper.Data;
using WebScraper.Models;
using WebScraper.Services;

namespace WebScraper.Api.Controllers;

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

    /// <summary>Get the latest push session checkpoint (for resume UI).</summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(DatabasePushSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DatabasePushSessionDto>> GetStatus(CancellationToken cancellationToken)
    {
        var session = await _pushService.GetSessionStatusAsync(_localDb, cancellationToken);
        if (session is null)
        {
            return Problem(
                title: "No push session",
                detail: "No push has been started yet.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(DatabasePushSessionDto.From(session));
    }

    /// <summary>
    /// Push local SQLite data to remote PostgreSQL in batched, resumable stages.
    /// Use <c>?resume=true</c> to continue an interrupted push, or <c>?reset=true</c> to start fresh.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ScrapeResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScrapeResult), StatusCodes.Status207MultiStatus)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ScrapeResult>> Push(
        [FromQuery] bool resume = false,
        [FromQuery] bool reset = false,
        CancellationToken cancellationToken = default)
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
        _logger.LogInformation("Push triggered by {User} (resume={Resume}, reset={Reset})", requestedBy, resume, reset);

        var result = await _pushService.PushToServerAsync(
            _localDb,
            connectionString,
            _display,
            new PushOptions { Resume = resume, Reset = reset },
            cancellationToken: cancellationToken);

        if (!result.Success)
            return StatusCode(StatusCodes.Status500InternalServerError, result);

        if (result.Errors.Count > 0)
            return StatusCode(StatusCodes.Status207MultiStatus, result);

        return Ok(result);
    }
}
