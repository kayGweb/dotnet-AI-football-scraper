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
