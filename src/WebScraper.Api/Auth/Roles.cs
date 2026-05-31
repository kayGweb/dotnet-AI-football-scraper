namespace WebScraper.Api.Auth;

/// <summary>
/// Centralised role name constants. Use these everywhere — typos in [Authorize(Roles=...)]
/// strings silently lock everyone out. Seeded by <see cref="IdentitySeeder"/> on startup.
/// </summary>
public static class Roles
{
    /// <summary>Full access: user management, key management, write endpoints, deleted-item restore.</summary>
    public const string Admin = "Admin";

    /// <summary>Can trigger scrapes and view jobs, but cannot manage users or keys.</summary>
    public const string Operator = "Operator";

    /// <summary>Read-only dashboard access. Equivalent to a scope=read API key, but via cookie/JWT.</summary>
    public const string Viewer = "Viewer";

    /// <summary>All defined roles, in seeding order.</summary>
    public static readonly string[] All = { Admin, Operator, Viewer };
}
