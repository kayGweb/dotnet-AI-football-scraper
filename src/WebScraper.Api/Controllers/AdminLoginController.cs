using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebScraper.Api.Auth;

namespace WebScraper.Api.Controllers;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class AdminLoginController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;

    public AdminLoginController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpPost("/admin/login-action")]
    public async Task<IActionResult> Login([FromForm] string email, [FromForm] string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return Redirect("/admin/login?error=invalid");

        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (result.IsLockedOut)
            return Redirect("/admin/login?error=locked");
        if (!result.Succeeded)
            return Redirect("/admin/login?error=invalid");

        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Email ?? user.Id),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var identity = new ClaimsIdentity(claims, AuthorizationPolicies.CookieSchemeName);
        await HttpContext.SignInAsync(
            AuthorizationPolicies.CookieSchemeName,
            new ClaimsPrincipal(identity));

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return Redirect("/admin");
    }

    [HttpGet("/admin/logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthorizationPolicies.CookieSchemeName);
        return Redirect("/admin/login");
    }
}
