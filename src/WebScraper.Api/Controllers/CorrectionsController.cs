using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebScraper.Api.Auth;
using WebScraper.Api.Dtos.Admin;
using WebScraper.Models;
using WebScraper.Services.Agent;

namespace WebScraper.Api.Controllers;

[ApiController]
[Route("api/v1/corrections")]
[Produces("application/json")]
public class CorrectionsController : ControllerBase
{
    private readonly DataCorrectionService _corrections;

    public CorrectionsController(DataCorrectionService corrections)
    {
        _corrections = corrections;
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.RequireOperate)]
    [ProducesResponseType(typeof(IEnumerable<DataCorrectionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DataCorrectionDto>>> List(
        [FromQuery] DataCorrectionStatus? status,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var items = await _corrections.ListAsync(status, limit, ct);
        return Ok(items.Select(c => c.ToDto()));
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.RequireApiAdmin)]
    [ProducesResponseType(typeof(DataCorrectionDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<DataCorrectionDto>> Propose(
        [FromBody] ProposeCorrectionRequest request,
        CancellationToken ct)
    {
        var proposedBy = User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("api_key_name")
            ?? "agent";

        var correction = await _corrections.ProposeAsync(
            request.EntityType,
            request.EntityId,
            request.Field,
            request.NewValue,
            request.Rationale,
            proposedBy,
            ct);

        return CreatedAtAction(nameof(List), new { id = correction.Id }, correction.ToDto());
    }

    [HttpPost("{id:long}/approve")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [ProducesResponseType(typeof(DataCorrectionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DataCorrectionDto>> Approve(long id, CancellationToken ct)
    {
        var resolvedBy = User.FindFirstValue(ClaimTypes.Email) ?? "admin";
        var correction = await _corrections.ApproveAsync(id, resolvedBy, ct);
        if (correction is null)
            return NotFound();

        return Ok(correction.ToDto());
    }

    [HttpPost("{id:long}/reject")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [ProducesResponseType(typeof(DataCorrectionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DataCorrectionDto>> Reject(long id, CancellationToken ct)
    {
        var resolvedBy = User.FindFirstValue(ClaimTypes.Email) ?? "admin";
        var correction = await _corrections.RejectAsync(id, resolvedBy, ct);
        if (correction is null)
            return NotFound();

        return Ok(correction.ToDto());
    }
}
