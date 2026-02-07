using Microsoft.EntityFrameworkCore;
using WebScraper.Models;

namespace WebScraper.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<PlayerGameStats> PlayerGameStats => Set<PlayerGameStats>();

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
    }
}
