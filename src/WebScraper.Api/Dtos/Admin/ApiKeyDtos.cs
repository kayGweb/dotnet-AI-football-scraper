using System.ComponentModel.DataAnnotations;

namespace WebScraper.Api.Dtos.Admin;

public class CreateApiKeyRequest
{
    [Required, MinLength(3), MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    /// <summary>e.g. ["read"], ["read","write"]. Defaults to ["read"] if empty.</summary>
    public List<string> Scopes { get; set; } = new() { "read" };

    /// <summary>Optional UTC expiry. Null = never expires.</summary>
    public DateTime? ExpiresAt { get; set; }
}

public class RevokeApiKeyRequest
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}

/// <summary>
/// Sent on creation only. <see cref="PlaintextKey"/> appears exactly once — the API will
/// never reveal it again. Admins must store it in their secrets manager immediately.
/// </summary>
public class ApiKeyCreatedDto
{
    public string KeyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string PlaintextKey { get; set; } = string.Empty;
}

/// <summary>Returned by list/get endpoints. Never includes plaintext or the hash.</summary>
public class ApiKeyDto
{
    public string KeyId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedBy { get; set; }
    public string? RevokeReason { get; set; }
}
