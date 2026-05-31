namespace WebScraper.Models;

/// <summary>
/// Database-backed API key for external consumers (the MCP server, CI jobs, Claude skills, etc.).
/// Replaces the file-based ApiKeyOptions list once chunk (a) of M3 ships — the SHA-256 hash
/// is the only thing persisted, so a database dump never exposes plaintext keys.
///
/// Lifecycle: created via POST /api/v1/api-keys (returns the plaintext once; admins must
/// copy it then), listed via GET (hash hidden), revoked via DELETE (soft delete via
/// ISoftDeletable so we keep the audit trail for ApiQueryLog joins).
/// </summary>
public class ApiKey : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }

    /// <summary>
    /// Opaque, URL-safe identifier surfaced in <see cref="ApiQueryLog.ApiKeyId"/>.
    /// Generated server-side as a short random string when the key is created.
    /// </summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>Human-friendly name shown in admin UI (e.g. "Claude MCP (primary)").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>SHA-256 hex digest of the plaintext key. Lowercase, no separators.</summary>
    public string HashedKey { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated scope list (e.g. "read", "read,write", "read,write,realtime").
    /// Stored as a flat string to keep the schema portable across providers.
    /// </summary>
    public string Scopes { get; set; } = "read";

    /// <summary>Identity of the admin user that created the key (Identity username).</summary>
    public string? CreatedBy { get; set; }

    /// <summary>UTC timestamp of the last successful authentication using this key. Null until first use.</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>Optional expiry. Keys past this date are rejected by the auth handler.</summary>
    public DateTime? ExpiresAt { get; set; }

    // --- IAuditableEntity ---
    public string? DataSource { get; set; }
    public DateTime? DataSourceFetchedAt { get; set; }
    public string? DataSourceRecordId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // --- ISoftDeletable ---
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public string? DeleteReason { get; set; }
}
