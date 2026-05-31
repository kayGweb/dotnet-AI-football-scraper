using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace WebScraper.Api.Auth;

/// <summary>
/// Centralised policy names and builders.
///
/// Two authentication schemes are wired up:
///   * <see cref="ApiKeyAuthenticationOptions.SchemeName"/> for external read consumers
///     (MCP server, CI jobs, Claude skills).
///   * <see cref="JwtBearerDefaults.AuthenticationScheme"/> for the admin dashboard +
///     write endpoints. Issued by <see cref="JwtTokenService"/> on login.
///
/// Policies pin each route to a specific scheme so an API key holder can't accidentally
/// authenticate against a JWT-only endpoint or vice versa.
/// </summary>
public static class AuthorizationPolicies
{
    public const string RequireReadScope = "RequireReadScope";
    public const string RequireAdmin = "RequireAdmin";
    public const string RequireOperator = "RequireOperator";
    public const string RequireViewer = "RequireViewer";

    public static void AddWebScraperApiAuthorization(this AuthorizationOptions options)
    {
        // API key with scope=read — covers all M1 read endpoints
        options.AddPolicy(RequireReadScope, policy =>
        {
            policy.AddAuthenticationSchemes(ApiKeyAuthenticationOptions.SchemeName);
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("scope", "read");
        });

        // JWT bearer + Admin role — user management, key management, deleted-item restore, push
        options.AddPolicy(RequireAdmin, policy =>
        {
            policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireRole(Roles.Admin);
        });

        // JWT bearer + Operator/Admin — scrape triggers + job inspection (M3 chunk b)
        options.AddPolicy(RequireOperator, policy =>
        {
            policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireRole(Roles.Admin, Roles.Operator);
        });

        // JWT bearer + any defined role — admin dashboard browsing
        options.AddPolicy(RequireViewer, policy =>
        {
            policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireRole(Roles.Admin, Roles.Operator, Roles.Viewer);
        });
    }
}
