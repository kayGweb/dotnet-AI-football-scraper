using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebScraper.Api.Auth;
using WebScraper.Api.Dtos.Admin;
using WebScraper.Api.Services;
using WebScraper.Models;

namespace WebScraper.Api.Controllers;

/// <summary>
/// Admin endpoints for managing database-backed API keys. The plaintext is shown exactly
/// once at creation time — see <see cref="ApiKeyManagementService"/>.
/// </summary>
[ApiController]
[Route("api/v1/api-keys")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
[Produces("application/json")]
public class ApiKeysController : ControllerBase
{
    private readonly ApiKeyManagementService _service;

    public ApiKeysController(ApiKeyManagementService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ApiKeyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ApiKeyDto>>> List(
        [FromQuery] bool includeRevoked = false,
        CancellationToken ct = default)
    {
        var keys = await _service.ListAsync(includeRevoked, ct);
        return Ok(keys.Select(ToDto));
    }

    [HttpGet("{keyId}")]
    [ProducesResponseType(typeof(ApiKeyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiKeyDto>> Get(string keyId, CancellationToken ct)
    {
        var key = await _service.GetAsync(keyId, ct);
        if (key is null)
        {
            return Problem(title: "API key not found", statusCode: StatusCodes.Status404NotFound);
        }
        return Ok(ToDto(key));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiKeyCreatedDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiKeyCreatedDto>> Create([FromBody] CreateApiKeyRequest request, CancellationToken ct)
    {
        var createdBy = User.Identity?.Name ?? "unknown";
        var created = await _service.CreateAsync(request.Name, request.Scopes, request.ExpiresAt, createdBy, ct);
        var dto = new ApiKeyCreatedDto
        {
            KeyId = created.Entity.KeyId,
            Name = created.Entity.Name,
            Scopes = created.Entity.Scopes,
            ExpiresAt = created.Entity.ExpiresAt,
            CreatedAt = created.Entity.CreatedAt,
            PlaintextKey = created.PlaintextKey,
        };
        return CreatedAtAction(nameof(Get), new { keyId = created.Entity.KeyId }, dto);
    }

    [HttpDelete("{keyId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(
        string keyId,
        [FromBody] RevokeApiKeyRequest? request,
        CancellationToken ct)
    {
        var revokedBy = User.Identity?.Name ?? "unknown";
        var ok = await _service.RevokeAsync(keyId, revokedBy, request?.Reason, ct);
        if (!ok)
        {
            return Problem(title: "API key not found", statusCode: StatusCodes.Status404NotFound);
        }
        return NoContent();
    }

    private static ApiKeyDto ToDto(ApiKey k) => new()
    {
        KeyId = k.KeyId,
        Name = k.Name,
        Scopes = k.Scopes,
        CreatedBy = k.CreatedBy,
        CreatedAt = k.CreatedAt,
        LastUsedAt = k.LastUsedAt,
        ExpiresAt = k.ExpiresAt,
        IsRevoked = k.IsDeleted,
        RevokedAt = k.DeletedAt,
        RevokedBy = k.DeletedBy,
        RevokeReason = k.DeleteReason,
    };
}
