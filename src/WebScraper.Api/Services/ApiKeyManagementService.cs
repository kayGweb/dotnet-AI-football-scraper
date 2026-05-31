using Microsoft.EntityFrameworkCore;
using WebScraper.Api.Auth;
using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Api.Services;

/// <summary>
/// Manages the lifecycle of database-backed API keys. The plaintext key is generated
/// and returned exactly once at creation time — only the SHA-256 hash is persisted, so
/// rotating a lost key requires creating a new one and revoking the old one.
/// </summary>
public class ApiKeyManagementService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ApiKeyManagementService> _logger;

    public ApiKeyManagementService(AppDbContext db, ILogger<ApiKeyManagementService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ApiKey>> ListAsync(bool includeRevoked, CancellationToken ct = default)
    {
        var query = includeRevoked
            ? _db.ApiKeys.IgnoreQueryFilters()
            : _db.ApiKeys.AsQueryable();

        return await query
            .AsNoTracking()
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<ApiKey?> GetAsync(string keyId, CancellationToken ct = default)
    {
        return await _db.ApiKeys
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyId == keyId, ct);
    }

    /// <summary>
    /// Creates a new API key. The returned <see cref="CreatedApiKey.PlaintextKey"/> is the
    /// only chance to capture the plaintext — it is NOT persisted.
    /// </summary>
    public async Task<CreatedApiKey> CreateAsync(
        string name,
        IEnumerable<string> scopes,
        DateTime? expiresAt,
        string createdBy,
        CancellationToken ct = default)
    {
        // 8-char shortid keeps log lines readable while remaining collision-safe at MVP scale.
        var keyId = $"k_{Convert.ToHexString(Guid.NewGuid().ToByteArray()).Substring(0, 8).ToLowerInvariant()}";
        var plaintext = $"sk_{ApiKeyHasher.GenerateRandomKey()}";
        var hash = ApiKeyHasher.Sha256Hex(plaintext);

        var scopeString = string.Join(",", scopes
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct());

        var entity = new ApiKey
        {
            KeyId = keyId,
            Name = name.Trim(),
            HashedKey = hash,
            Scopes = string.IsNullOrEmpty(scopeString) ? "read" : scopeString,
            CreatedBy = createdBy,
            ExpiresAt = expiresAt,
        };

        _db.ApiKeys.Add(entity);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created API key {KeyId} ({Name}) for {CreatedBy} with scopes {Scopes}",
            keyId, name, createdBy, entity.Scopes);

        return new CreatedApiKey(entity, plaintext);
    }

    /// <summary>
    /// Soft-deletes (revokes) the key. The interceptor handles IsDeleted + DeletedAt; we
    /// stamp DeletedBy/DeleteReason ourselves so the audit trail is meaningful.
    /// </summary>
    public async Task<bool> RevokeAsync(string keyId, string revokedBy, string? reason, CancellationToken ct = default)
    {
        var entity = await _db.ApiKeys.FirstOrDefaultAsync(k => k.KeyId == keyId, ct);
        if (entity is null) return false;

        entity.DeletedBy = revokedBy;
        entity.DeleteReason = reason;
        _db.ApiKeys.Remove(entity);   // interceptor converts to soft delete
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Revoked API key {KeyId} by {RevokedBy} (reason: {Reason})", keyId, revokedBy, reason ?? "<none>");
        return true;
    }
}

public record CreatedApiKey(ApiKey Entity, string PlaintextKey);
