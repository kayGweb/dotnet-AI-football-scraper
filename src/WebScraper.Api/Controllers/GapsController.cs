using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebScraper.Api.Auth;
using WebScraper.Services.Agent;

namespace WebScraper.Api.Controllers;

[ApiController]
[Route("api/v1/gaps")]
[Authorize(Policy = AuthorizationPolicies.RequireOperateScope)]
[Produces("application/json")]
public class GapsController : ControllerBase
{
    private readonly GapFinderService _gaps;

    public GapsController(GapFinderService gaps)
    {
        _gaps = gaps;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GapItem>>> FindGaps(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var items = await _gaps.FindGapsAsync(limit, ct);
        return Ok(items);
    }
}
