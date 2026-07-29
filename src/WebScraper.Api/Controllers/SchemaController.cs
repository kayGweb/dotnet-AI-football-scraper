using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebScraper.Api.Auth;
using WebScraper.Services.Agent;

namespace WebScraper.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class SchemaController : ControllerBase
{
    [HttpGet("schema")]
    [Authorize(Policy = AuthorizationPolicies.RequireReadScope)]
    public ActionResult<object> DescribeSchema([FromQuery] string? entity)
        => Ok(SchemaDescriptionService.Describe(entity));

    [HttpGet("schema/dictionary")]
    [Authorize(Policy = AuthorizationPolicies.RequireReadScope)]
    public ActionResult<object> GetDataDictionary()
        => Ok(DataDictionaryService.GetDictionary());

    [HttpPost("query/stats")]
    [Authorize(Policy = AuthorizationPolicies.RequireReadScope)]
    public async Task<ActionResult<object>> QueryStats(
        [FromBody] QueryStatsRequest request,
        [FromServices] QueryStatsService queryStats,
        CancellationToken ct)
    {
        var result = await queryStats.ExecuteAsync(request, ct);
        return Ok(result);
    }
}
