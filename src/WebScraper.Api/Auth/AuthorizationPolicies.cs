using Microsoft.AspNetCore.Authorization;

namespace WebScraper.Api.Auth;

/// <summary>
/// Centralised policy names and builders. M1 only needs a single read-scope
/// policy over the API key scheme — JWT + role policies land in M3 when write
/// endpoints come online.
/// </summary>
public static class AuthorizationPolicies
{
    public const string RequireReadScope = "RequireReadScope";

    public static void AddWebScraperApiAuthorization(this AuthorizationOptions options)
    {
        options.AddPolicy(RequireReadScope, policy =>
        {
            policy.AddAuthenticationSchemes(ApiKeyAuthenticationOptions.SchemeName);
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("scope", "read");
        });
    }
}
