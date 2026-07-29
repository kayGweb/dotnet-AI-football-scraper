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
    public const string RequireOperateScope = "RequireOperateScope";
    public const string RequireApiAdminScope = "RequireApiAdminScope";
    /// <summary>API key operate/admin scope OR JWT Operator/Admin — scrape, jobs, coverage.</summary>
    public const string RequireOperate = "RequireOperate";
    /// <summary>API key admin scope OR JWT Admin — correction proposals and approval.</summary>
    public const string RequireApiAdmin = "RequireApiAdmin";
    public const string RequireAdmin = "RequireAdmin";
    public const string RequireOperator = "RequireOperator";
    public const string RequireViewer = "RequireViewer";

    public const string CookieSchemeName = "AdminCookie";

    public static void AddWebScraperApiAuthorization(this AuthorizationOptions options)
    {
        // API key with scope=read — covers all M1 read endpoints
        options.AddPolicy(RequireReadScope, policy =>
        {
            policy.AddAuthenticationSchemes(ApiKeyAuthenticationOptions.SchemeName);
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("scope", "read", "operate", "admin");
        });

        // API key with scope=operate or admin
        options.AddPolicy(RequireOperateScope, policy =>
        {
            policy.AddAuthenticationSchemes(ApiKeyAuthenticationOptions.SchemeName);
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("scope", "operate", "admin");
        });

        // API key with scope=admin only (mutation proposals)
        options.AddPolicy(RequireApiAdminScope, policy =>
        {
            policy.AddAuthenticationSchemes(ApiKeyAuthenticationOptions.SchemeName);
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("scope", "admin");
        });

        // Operate tier: API key (operate/admin) OR JWT Operator/Admin
        options.AddPolicy(RequireOperate, policy =>
        {
            policy.AddAuthenticationSchemes(
                ApiKeyAuthenticationOptions.SchemeName,
                JwtBearerDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx => HasApiScope(ctx, "operate", "admin")
                || ctx.User.IsInRole(Roles.Admin)
                || ctx.User.IsInRole(Roles.Operator));
        });

        // Mutate tier proposals: API key admin OR JWT Admin
        options.AddPolicy(RequireApiAdmin, policy =>
        {
            policy.AddAuthenticationSchemes(
                ApiKeyAuthenticationOptions.SchemeName,
                JwtBearerDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx => HasApiScope(ctx, "admin")
                || ctx.User.IsInRole(Roles.Admin));
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
        // Cookie + any dashboard role — Blazor admin pages
        options.AddPolicy("DashboardAccess", policy =>
        {
            policy.AddAuthenticationSchemes(CookieSchemeName);
            policy.RequireAuthenticatedUser();
            policy.RequireRole(Roles.Admin, Roles.Operator, Roles.Viewer);
        });
    }

    private static bool HasApiScope(AuthorizationHandlerContext ctx, params string[] scopes)
    {
        if (!ctx.User.HasClaim(c => c.Type == "scope"))
            return false;

        var userScopes = ctx.User.FindAll("scope").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return scopes.Any(s => userScopes.Contains(s));
    }
}
