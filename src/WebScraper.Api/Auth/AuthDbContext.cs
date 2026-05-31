using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace WebScraper.Api.Auth;

/// <summary>
/// Separate DbContext for ASP.NET Core Identity tables (AspNetUsers, AspNetRoles, etc.).
/// Lives in the API project so the Core library stays free of an Identity dependency —
/// the CLI doesn't need user management. Shares the underlying database/connection with
/// <see cref="WebScraper.Data.AppDbContext"/> but uses its own __EFMigrationsHistory table
/// (suffix configured below) so migrations stay independent.
/// </summary>
public class AuthDbContext : IdentityDbContext<AppUser>
{
    public const string MigrationsHistoryTable = "__AuthMigrationsHistory";

    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Prefix Identity tables so they're visibly distinct from the domain schema
        // when admins poke at the DB directly. Functional impact is zero.
        builder.Entity<AppUser>().ToTable("Auth_Users");
        builder.Entity<IdentityRole>().ToTable("Auth_Roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("Auth_UserRoles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("Auth_UserClaims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("Auth_UserLogins");
        builder.Entity<IdentityUserToken<string>>().ToTable("Auth_UserTokens");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("Auth_RoleClaims");
    }
}
