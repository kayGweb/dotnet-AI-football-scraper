using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WebScraper.Models;

namespace WebScraper.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<PlayerGameStats> PlayerGameStats => Set<PlayerGameStats>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<TeamGameStats> TeamGameStats => Set<TeamGameStats>();
    public DbSet<Injury> Injuries => Set<Injury>();
    public DbSet<ApiLink> ApiLinks => Set<ApiLink>();

    /// <summary>
    /// Observability log of every public API request. Written asynchronously by the
    /// ApiQueryLoggingMiddleware (Phase 1) via a background Channel writer so the
    /// hot path never blocks on the DB.
    /// </summary>
    public DbSet<ApiQueryLog> ApiQueryLogs => Set<ApiQueryLog>();

    /// <summary>
    /// Database-backed API keys (M3). Replaces the file-based ApiKeyOptions list.
    /// Admin endpoints under /api/v1/api-keys manage lifecycle.
    /// </summary>
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Game has two FKs to Team — must use Restrict to avoid cascade cycles
        modelBuilder.Entity<Game>()
            .HasOne(g => g.HomeTeam)
            .WithMany(t => t.HomeGames)
            .HasForeignKey(g => g.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Game>()
            .HasOne(g => g.AwayTeam)
            .WithMany(t => t.AwayGames)
            .HasForeignKey(g => g.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // Game -> Venue (optional)
        modelBuilder.Entity<Game>()
            .HasOne(g => g.Venue)
            .WithMany(v => v.Games)
            .HasForeignKey(g => g.VenueId)
            .IsRequired(false);

        // PlayerGameStats -> Player
        modelBuilder.Entity<PlayerGameStats>()
            .HasOne(s => s.Player)
            .WithMany(p => p.GameStats)
            .HasForeignKey(s => s.PlayerId);

        // PlayerGameStats -> Game
        modelBuilder.Entity<PlayerGameStats>()
            .HasOne(s => s.Game)
            .WithMany(g => g.PlayerStats)
            .HasForeignKey(s => s.GameId);

        // Player -> Team (optional)
        modelBuilder.Entity<Player>()
            .HasOne(p => p.Team)
            .WithMany(t => t.Players)
            .HasForeignKey(p => p.TeamId)
            .IsRequired(false);

        // TeamGameStats -> Game
        modelBuilder.Entity<TeamGameStats>()
            .HasOne(tgs => tgs.Game)
            .WithMany(g => g.TeamStats)
            .HasForeignKey(tgs => tgs.GameId);

        // TeamGameStats -> Team
        modelBuilder.Entity<TeamGameStats>()
            .HasOne(tgs => tgs.Team)
            .WithMany(t => t.TeamStats)
            .HasForeignKey(tgs => tgs.TeamId);

        // TeamGameStats unique index: one row per team per game
        modelBuilder.Entity<TeamGameStats>()
            .HasIndex(tgs => new { tgs.GameId, tgs.TeamId })
            .IsUnique();

        // Injury -> Game
        modelBuilder.Entity<Injury>()
            .HasOne(i => i.Game)
            .WithMany(g => g.Injuries)
            .HasForeignKey(i => i.GameId);

        // Injury -> Player (optional)
        modelBuilder.Entity<Injury>()
            .HasOne(i => i.Player)
            .WithMany(p => p.Injuries)
            .HasForeignKey(i => i.PlayerId)
            .IsRequired(false);

        // Injury unique index: one injury record per athlete per game
        modelBuilder.Entity<Injury>()
            .HasIndex(i => new { i.GameId, i.EspnAthleteId })
            .IsUnique();

        // ApiLink -> Game (optional)
        modelBuilder.Entity<ApiLink>()
            .HasOne(al => al.Game)
            .WithMany(g => g.ApiLinks)
            .HasForeignKey(al => al.GameId)
            .IsRequired(false);

        // ApiLink -> Team (optional)
        modelBuilder.Entity<ApiLink>()
            .HasOne(al => al.Team)
            .WithMany(t => t.ApiLinks)
            .HasForeignKey(al => al.TeamId)
            .IsRequired(false);

        // ApiLink unique index on Url
        modelBuilder.Entity<ApiLink>()
            .HasIndex(al => al.Url)
            .IsUnique();

        // Venue unique index on EspnId
        modelBuilder.Entity<Venue>()
            .HasIndex(v => v.EspnId)
            .IsUnique();

        // ApiQueryLog — observability index for dashboard queries
        modelBuilder.Entity<ApiQueryLog>()
            .HasIndex(q => q.Timestamp);
        modelBuilder.Entity<ApiQueryLog>()
            .HasIndex(q => new { q.ApiKeyId, q.Timestamp });

        // ApiKey — unique index on KeyId so the auth handler can do a single point lookup
        modelBuilder.Entity<ApiKey>()
            .HasIndex(k => k.KeyId)
            .IsUnique();
        // Lookup-by-hash on the hot auth path
        modelBuilder.Entity<ApiKey>()
            .HasIndex(k => k.HashedKey);

        // Global soft-delete query filters: any entity implementing ISoftDeletable is
        // automatically hidden from normal queries. Admin code uses IgnoreQueryFilters()
        // to see deleted rows in the review UI.
        modelBuilder.Entity<Team>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Player>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Game>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PlayerGameStats>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Venue>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TeamGameStats>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Injury>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ApiLink>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ApiKey>().HasQueryFilter(e => !e.IsDeleted);

        // Ensure all DateTime properties are stored as UTC for PostgreSQL compatibility
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(utcConverter);
                }
            }
        }
    }
}
