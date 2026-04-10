using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace WebScraper.Api.Auth;

/// <summary>
/// Custom AuthenticationHandler that validates incoming requests by comparing the
/// SHA-256 hash of the <c>X-Api-Key</c> header against the hashes configured in
/// <see cref="ApiKeyOptions"/>. On success, emits a ClaimsPrincipal carrying the
/// key id, name, and scopes — consumed by the <see cref="AuthorizationPolicies"/>
/// and surfaced on <see cref="HttpContext.User"/> so downstream middleware (e.g.
/// <see cref="Middleware.ApiQueryLoggingMiddleware"/>) can identify the caller.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public const string HeaderName = "X-Api-Key";

    private readonly IOptionsMonitor<ApiKeyOptions> _apiKeyOptions;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptionsMonitor<ApiKeyOptions> apiKeyOptions)
        : base(options, logger, encoder)
    {
        _apiKeyOptions = apiKeyOptions;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            // No header → let the framework fall through to other handlers / challenge.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedKey = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Empty API key."));
        }

        var providedHash = Sha256Hex(providedKey);
        var match = _apiKeyOptions.CurrentValue.Keys.FirstOrDefault(k =>
            !string.IsNullOrEmpty(k.HashedKey) &&
            CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(k.HashedKey.ToLowerInvariant()),
                Encoding.ASCII.GetBytes(providedHash)));

        if (match is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, match.Id),
            new(ClaimTypes.Name, match.Name),
            new("api_key_id", match.Id),
            new("api_key_name", match.Name),
        };
        foreach (var scope in match.Scopes)
        {
            claims.Add(new Claim("scope", scope));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers["WWW-Authenticate"] = $"ApiKey realm=\"WebScraper.Api\"";
        return Task.CompletedTask;
    }

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
}
