using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WebScraper.Models;

namespace WebScraper.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Franchise> Franchises => Set<Franchise>();
    public DbSet<TeamSeason> TeamSeasons => Set<TeamSeason>();
    public DbSet<PlayerTeamSeason> PlayerTeamSeasons => Set<PlayerTeamSeason>();
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

    /// <summary>
    /// Persisted scrape jobs (M3 chunk b). Each POST /api/v1/scrape/* creates a row,
    /// enqueues the ID, and the ScrapeJobWorker picks it up. Survives restarts.
    /// </summary>
    public DbSet<ScrapeJob> ScrapeJobs => Set<ScrapeJob>();

    /// <summary>
    /// Outbox of scrape lifecycle events (M3 chunk c). Written transactionally with
    /// ScrapeJob state changes; the ScrapeEventRelay broadcasts them via SignalR and
    /// the /api/v1/events?since= endpoint replays missed events for reconnecting clients.
    /// </summary>
    public DbSet<ScrapeEvent> ScrapeEvents => Set<ScrapeEvent>();

    /// <summary>Expected-vs-actual coverage per (season, seasonType, week). Phase B.</summary>
    public DbSet<SeasonCoverage> SeasonCoverages => Set<SeasonCoverage>();

    /// <summary>Data quality findings from post-scrape rules. Phase B.</summary>
    public DbSet<DataQualityFinding> DataQualityFindings => Set<DataQualityFinding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Game has two FKs to TeamSeason — must use Restrict to avoid cascade cycles
        modelBuilder.Entity<Game>()
            .HasOne(g => g.HomeTeamSeason)
            .WithMany(ts => ts.HomeGames)
            .HasForeignKey(g => g.HomeTeamSeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Game>()
            .HasOne(g => g.AwayTeamSeason)
            .WithMany(ts => ts.AwayGames)
            .HasForeignKey(g => g.AwayTeamSeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Game>()
            .HasIndex(g => new { g.Season, g.SeasonType, g.Week, g.HomeTeamSeasonId, g.AwayTeamSeasonId })
            .IsUnique();

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

        // TeamGameStats -> TeamSeason
        modelBuilder.Entity<TeamGameStats>()
            .HasOne(tgs => tgs.TeamSeason)
            .WithMany(ts => ts.TeamStats)
            .HasForeignKey(tgs => tgs.TeamSeasonId);

        // TeamGameStats unique index: one row per team-season per game
        modelBuilder.Entity<TeamGameStats>()
            .HasIndex(tgs => new { tgs.GameId, tgs.TeamSeasonId })
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

        // Franchise unique canonical abbreviation
        modelBuilder.Entity<Franchise>()
            .HasIndex(f => f.CanonicalAbbreviation)
            .IsUnique();

        // TeamSeason unique per franchise per season
        modelBuilder.Entity<TeamSeason>()
            .HasIndex(ts => new { ts.FranchiseId, ts.Season })
            .IsUnique();

        modelBuilder.Entity<TeamSeason>()
            .HasOne(ts => ts.Franchise)
            .WithMany(f => f.TeamSeasons)
            .HasForeignKey(ts => ts.FranchiseId);

        // PlayerTeamSeason
        modelBuilder.Entity<PlayerTeamSeason>()
            .HasIndex(pts => new { pts.PlayerId, pts.TeamSeasonId })
            .IsUnique();

        modelBuilder.Entity<PlayerTeamSeason>()
            .HasOne(pts => pts.Player)
            .WithMany()
            .HasForeignKey(pts => pts.PlayerId);

        modelBuilder.Entity<PlayerTeamSeason>()
            .HasOne(pts => pts.TeamSeason)
            .WithMany(ts => ts.PlayerRosterEntries)
            .HasForeignKey(pts => pts.TeamSeasonId);

        // Player unique EspnId when present
        modelBuilder.Entity<Player>()
            .HasIndex(p => p.EspnId)
            .IsUnique()
            .HasFilter("\"EspnId\" IS NOT NULL");

        // ScrapeJob parent/child for backfill fan-out
        modelBuilder.Entity<ScrapeJob>()
            .HasIndex(j => j.ParentJobId);

        modelBuilder.Entity<ScrapeJob>()
            .HasIndex(j => j.DependsOnJobId);

        // SeasonCoverage unique per week
        modelBuilder.Entity<SeasonCoverage>()
            .HasIndex(c => new { c.Season, c.SeasonType, c.Week })
            .IsUnique();

        modelBuilder.Entity<SeasonCoverage>()
            .HasIndex(c => c.Season);

        // DataQualityFinding indexes for dashboard queries
        modelBuilder.Entity<DataQualityFinding>()
            .HasIndex(f => new { f.Status, f.Severity });

        modelBuilder.Entity<DataQualityFinding>()
            .HasIndex(f => new { f.RuleType, f.EntityType, f.EntityId });

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

        // ScrapeJob — index on Status + CreatedAt for the worker's startup recovery query
        modelBuilder.Entity<ScrapeJob>()
            .HasIndex(j => new { j.Status, j.CreatedAt });

        // ScrapeEvent — JobId index for per-job timelines; the relay polls by Id (PK) directly
        modelBuilder.Entity<ScrapeEvent>()
            .HasIndex(e => e.JobId);

        // Global soft-delete query filters: any entity implementing ISoftDeletable is
        // automatically hidden from normal queries. Admin code uses IgnoreQueryFilters()
        // to see deleted rows in the review UI.
        modelBuilder.Entity<Team>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Franchise>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TeamSeason>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PlayerTeamSeason>().HasQueryFilter(e => !e.IsDeleted);
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
