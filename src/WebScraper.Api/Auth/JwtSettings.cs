namespace WebScraper.Api.Auth;

/// <summary>
/// Bound from the "Jwt" section of configuration. The signing key must come from
/// appsettings.Local.json (git-ignored) or an environment variable in production —
/// the value in checked-in appsettings.json is a placeholder that fails validation at startup.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>Issuer claim — typically the API's base URL.</summary>
    public string Issuer { get; set; } = "WebScraper.Api";

    /// <summary>Audience claim — typically the client(s) consuming the JWT.</summary>
    public string Audience { get; set; } = "WebScraper.Clients";

    /// <summary>
    /// Symmetric signing key. Must be at least 32 bytes (256 bits) for HMAC-SHA256.
    /// Generate locally with: <c>openssl rand -base64 48</c>.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Token lifetime in minutes. Default 60.</summary>
    public int AccessTokenMinutes { get; set; } = 60;
}
