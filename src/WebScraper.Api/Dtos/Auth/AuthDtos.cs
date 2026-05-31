using System.ComponentModel.DataAnnotations;

namespace WebScraper.Api.Dtos.Auth;

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
}

public class RegisterUserRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Role to assign. Must be one of <see cref="WebScraper.Api.Auth.Roles.All"/>.
    /// Defaults to "Viewer" if omitted.
    /// </summary>
    public string Role { get; set; } = "Viewer";
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
    public DateTime? LastLoginAt { get; set; }
}
