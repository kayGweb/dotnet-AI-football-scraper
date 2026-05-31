namespace WebScraper.Api.Auth;

/// <summary>
/// Binds the "InitialAdmin" section. If both Email and Password are set and no users
/// exist yet, <see cref="IdentitySeeder"/> creates an initial Admin account on startup
/// so a fresh install can log in. Production: set these via env vars and rotate the
/// password from the dashboard immediately after first login.
/// </summary>
public class InitialAdminSettings
{
    public const string SectionName = "InitialAdmin";

    public string? Email { get; set; }
    public string? Password { get; set; }
}
