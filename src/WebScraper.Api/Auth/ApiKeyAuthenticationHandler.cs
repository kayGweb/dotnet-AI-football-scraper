using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebScraper.Data;

namespace WebScraper.Api.Auth;

/// <summary>
/// Validates incoming requests by SHA-256 hashing the <c>X-Api-Key</c> header and looking
/// it up first in <see cref="AppDbContext.ApiKeys"/> (DB-backed, lifecycle managed via the
/// admin endpoints), then falling back to <see cref="ApiKeyOptions"/> for the bootstrap
/// key configured in appsettings (so a fresh install isn't locked out before the first
/// admin user creates a DB key).
///
/// On success, emits a ClaimsPrincipal carrying the key id, name, and scopes — consumed
/// by <see cref="AuthorizationPolicies"/> and surfaced on <see cref="HttpContext.User"/>
/// so downstream middleware (e.g. <see cref="Middleware.ApiQueryLoggingMiddleware"/>) can
/// identify the caller.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public const string HeaderName = "X-Api-Key";

    private readonly IOptionsMonitor<ApiKeyOptions> _apiKeyOptions;
    private readonly IServiceScopeFactory _scopeFactory;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptionsMonitor<ApiKeyOptions> apiKeyOptions,
        IServiceScopeFactory scopeFactory)
        : base(options, logger, encoder)
    {
        _apiKeyOptions = apiKeyOptions;
        _scopeFactory = scopeFactory;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            return AuthenticateResult.NoResult();
        }

        var providedKey = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return AuthenticateResult.Fail("Empty API key.");
        }

        var providedHash = ApiKeyHasher.Sha256Hex(providedKey);

        // ---- 1. DB lookup (managed via admin endpoints) ----
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Index on HashedKey makes this a single point lookup.
            var dbMatch = await db.ApiKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.HashedKey == providedHash);

            if (dbMatch is not null)
            {
                if (dbMatch.ExpiresAt is { } exp && exp < DateTime.UtcNow)
                {
                    return AuthenticateResult.Fail("API key expired.");
                }

                // Best-effort LastUsedAt update — fire and forget, never block auth on it.
                _ = UpdateLastUsedAsync(dbMatch.Id);

                var scopes = (dbMatch.Scopes ?? "read")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                return BuildSuccess(dbMatch.KeyId, dbMatch.Name, scopes);
            }
        }

        // ---- 2. Config fallback (bootstrap key) ----
        var configMatch = _apiKeyOptions.CurrentValue.Keys.FirstOrDefault(k =>
            ApiKeyHasher.ConstantTimeEquals(k.HashedKey, providedHash));

        if (configMatch is not null)
        {
            return BuildSuccess(configMatch.Id, configMatch.Name, configMatch.Scopes);
        }

        return AuthenticateResult.Fail("Invalid API key.");
    }

    private AuthenticateResult BuildSuccess(string keyId, string keyName, IEnumerable<string> scopes)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, keyId),
            new(ClaimTypes.Name, keyName),
            new("api_key_id", keyId),
            new("api_key_name", keyName),
        };
        foreach (var s in scopes)
        {
            claims.Add(new Claim("scope", s));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    private async Task UpdateLastUsedAsync(int id)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Skip the interceptor's UpdatedAt stamp — LastUsedAt is the only thing changing.
            await db.ApiKeys
                .Where(k => k.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            // Hot path: never let a stats write break authentication.
            Logger.LogDebug(ex, "Failed to update ApiKey.LastUsedAt for key {Id}", id);
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers["WWW-Authenticate"] = "ApiKey realm=\"WebScraper.Api\"";
        return Task.CompletedTask;
    }
}

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
}
