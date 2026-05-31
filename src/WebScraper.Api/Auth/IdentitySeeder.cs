using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace WebScraper.Api.Auth;

/// <summary>
/// Idempotent startup seeder: ensures every role in <see cref="Roles.All"/> exists, and
/// creates an initial Admin user from <see cref="InitialAdminSettings"/> if the user
/// table is empty. Called from Program.cs after migrations run.
/// </summary>
public class IdentitySeeder
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly InitialAdminSettings _initialAdmin;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        RoleManager<IdentityRole> roleManager,
        UserManager<AppUser> userManager,
        IOptions<InitialAdminSettings> initialAdmin,
        ILogger<IdentitySeeder> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _initialAdmin = initialAdmin.Value;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        foreach (var role in Roles.All)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                var result = await _roleManager.CreateAsync(new IdentityRole(role));
                if (result.Succeeded)
                {
                    _logger.LogInformation("Seeded role {Role}", role);
                }
                else
                {
                    _logger.LogWarning("Failed to seed role {Role}: {Errors}",
                        role, string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        // Initial admin: only when no users exist AND both config values are set.
        // We deliberately don't auto-create on every startup so admin can rotate the
        // password and not have it overwritten on next boot.
        if (string.IsNullOrWhiteSpace(_initialAdmin.Email) || string.IsNullOrWhiteSpace(_initialAdmin.Password))
        {
            return;
        }

        var anyUser = await _userManager.Users.AnyAsync();
        if (anyUser) return;

        var admin = new AppUser
        {
            UserName = _initialAdmin.Email,
            Email = _initialAdmin.Email,
            EmailConfirmed = true,
        };

        var create = await _userManager.CreateAsync(admin, _initialAdmin.Password);
        if (!create.Succeeded)
        {
            _logger.LogError("Failed to create initial admin {Email}: {Errors}",
                _initialAdmin.Email, string.Join("; ", create.Errors.Select(e => e.Description)));
            return;
        }

        var assign = await _userManager.AddToRoleAsync(admin, Roles.Admin);
        if (!assign.Succeeded)
        {
            _logger.LogError("Created initial admin but failed to assign Admin role: {Errors}",
                string.Join("; ", assign.Errors.Select(e => e.Description)));
            return;
        }

        _logger.LogWarning(
            "Initial admin user {Email} created. Rotate the password from /api/v1/auth ASAP — " +
            "the bootstrap password lives in configuration.", _initialAdmin.Email);
    }
}
