using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebScraper.Api.Auth;
using WebScraper.Api.Dtos.Auth;

namespace WebScraper.Api.Controllers;

/// <summary>
/// JWT login + user management. Login is anonymous; the management endpoints require Admin.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly JwtTokenService _tokens;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        JwtTokenService tokens,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokens = tokens;
        _logger = logger;
    }

    /// <summary>Exchange email + password for a JWT bearer token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // Constant-message error — don't leak which half of the credential was wrong.
            return Problem(
                title: "Invalid credentials",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var check = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            _logger.LogInformation("Login failed for {Email} (locked={Locked})", request.Email, check.IsLockedOut);
            return Problem(
                title: check.IsLockedOut ? "Account locked" : "Invalid credentials",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var (token, expiresAt) = await _tokens.IssueAsync(user);
        var roles = await _userManager.GetRolesAsync(user);

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return Ok(new LoginResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            Roles = roles,
        });
    }

    /// <summary>Returns the calling user's profile + roles. Useful for client-side bootstrapping.</summary>
    [HttpGet("me")]
    [Authorize(Policy = AuthorizationPolicies.RequireViewer)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var roles = await _userManager.GetRolesAsync(user);
        return Ok(new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            Roles = roles,
            LastLoginAt = user.LastLoginAt,
        });
    }

    /// <summary>Create a new user with a specific role (Admin only).</summary>
    [HttpPost("users")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] RegisterUserRequest request)
    {
        if (!Roles.All.Contains(request.Role))
        {
            return Problem(
                title: "Invalid role",
                detail: $"Role must be one of: {string.Join(", ", Roles.All)}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
        };

        var create = await _userManager.CreateAsync(user, request.Password);
        if (!create.Succeeded)
        {
            return ValidationProblem(
                detail: string.Join("; ", create.Errors.Select(e => e.Description)),
                statusCode: StatusCodes.Status400BadRequest);
        }

        var assign = await _userManager.AddToRoleAsync(user, request.Role);
        if (!assign.Succeeded)
        {
            // Roll back the user so we don't end up with role-less ghosts.
            await _userManager.DeleteAsync(user);
            return ValidationProblem(
                detail: string.Join("; ", assign.Errors.Select(e => e.Description)),
                statusCode: StatusCodes.Status400BadRequest);
        }

        return CreatedAtAction(nameof(Me), new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            Roles = new List<string> { request.Role },
        });
    }

    /// <summary>List all users (Admin only).</summary>
    [HttpGet("users")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UserDto>>> ListUsers()
    {
        var users = await _userManager.Users.ToListAsync();
        var dtos = new List<UserDto>(users.Count);
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            dtos.Add(new UserDto
            {
                Id = u.Id,
                Email = u.Email ?? string.Empty,
                Roles = roles,
                LastLoginAt = u.LastLoginAt,
            });
        }
        return Ok(dtos);
    }
}
