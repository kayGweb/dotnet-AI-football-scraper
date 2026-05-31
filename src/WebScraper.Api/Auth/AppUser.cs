using Microsoft.AspNetCore.Identity;

namespace WebScraper.Api.Auth;

/// <summary>
/// Identity user for the admin dashboard + JWT-protected write endpoints. Kept thin
/// on purpose — extend with profile fields (DisplayName, default ScraperSource preference,
/// etc.) only when a UI actually needs them.
/// </summary>
public class AppUser : IdentityUser
{
    /// <summary>UTC timestamp of the most recent successful login. Null until first login.</summary>
    public DateTime? LastLoginAt { get; set; }
}
