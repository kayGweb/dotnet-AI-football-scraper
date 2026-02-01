# NFL Web Scraper - MVP Specification

## Project Overview

A .NET 8 Core Console application that scrapes NFL football data from public sources and stores it in a structured database for analysis and querying.

---

## Goals

- Scrape NFL game data, player stats, and team information
- Store data in a normalized relational database
- Provide a clean, repeatable data pipeline
- Handle rate limiting and respectful scraping practices

---

## Data Sources

| Source | URL | Data Available |
|--------|-----|----------------|
| Pro Football Reference | pro-football-reference.com | Historical stats, box scores, player data |
| ESPN | espn.com/nfl | Schedules, scores, rosters |
| NFL.com | nfl.com/stats | Official stats, standings |

> **Note:** Always check `robots.txt` and terms of service before scraping.

---

## Data to Collect (MVP Scope)

### Teams
- Team name, abbreviation, division, conference
- Current season record

### Players
- Name, position, team, jersey number
- Basic bio (height, weight, college)

### Games
- Date, home team, away team
- Final score, quarter scores
- Season, week number

### Player Game Stats
- Passing: attempts, completions, yards, TDs, INTs
- Rushing: attempts, yards, TDs
- Receiving: targets, receptions, yards, TDs

---

## Tech Stack

| Component | Technology | Rationale |
|-----------|------------|-----------|
| Framework | .NET 8 Core | Long-term support, performance |
| App Type | Console Application | Simple CLI-based execution |
| HTTP Client | `HttpClient` / `IHttpClientFactory` | Built-in, async support |
| HTML Parser | `HtmlAgilityPack` | Robust HTML/DOM parsing |
| Database | SQLite / PostgreSQL / SQL Server | Swappable via configuration |
| ORM | Entity Framework Core 8 | Microsoft's standard ORM |
| Data Access | Repository Pattern | Database provider abstraction |
| DI Container | Microsoft.Extensions.DependencyInjection | Built-in DI support |
| Config | `appsettings.json` + User Secrets | .NET standard configuration |
| Logging | Serilog | Structured logging |

---

## Project Structure

```
WebScraper/
├── src/
│   └── WebScraper/
│       ├── WebScraper.csproj
│       ├── Program.cs                 # Entry point
│       ├── appsettings.json           # Configuration
│       ├── Models/
│       │   ├── Team.cs
│       │   ├── Player.cs
│       │   ├── Game.cs
│       │   └── PlayerGameStats.cs
│       ├── Data/
│       │   ├── AppDbContext.cs        # EF Core DbContext
│       │   └── Repositories/
│       │       ├── IRepository.cs
│       │       ├── TeamRepository.cs
│       │       ├── PlayerRepository.cs
│       │       ├── GameRepository.cs
│       │       └── StatsRepository.cs
│       ├── Services/
│       │   ├── Scrapers/
│       │   │   ├── IScraperService.cs
│       │   │   ├── BaseScraperService.cs
│       │   │   ├── TeamScraperService.cs
│       │   │   ├── PlayerScraperService.cs
│       │   │   ├── GameScraperService.cs
│       │   │   └── StatsScraperService.cs
│       │   └── RateLimiterService.cs
│       └── Extensions/
│           └── ServiceCollectionExtensions.cs
├── tests/
│   └── WebScraper.Tests/
│       ├── WebScraper.Tests.csproj
│       └── ...
├── data/
│   └── nfl_data.db                    # SQLite database
├── WebScraper.sln
├── .gitignore
└── AGENT_MVP.md
```

---

## Database Schema

### teams
| Column | Type | Description |
|--------|------|-------------|
| id | INTEGER PK | Auto-increment |
| name | VARCHAR(100) | Full team name |
| abbreviation | VARCHAR(5) | e.g., "KC", "SF" |
| city | VARCHAR(100) | Team city |
| conference | VARCHAR(10) | AFC / NFC |
| division | VARCHAR(20) | North/South/East/West |

### players
| Column | Type | Description |
|--------|------|-------------|
| id | INTEGER PK | Auto-increment |
| name | VARCHAR(200) | Full name |
| team_id | INTEGER FK | Reference to teams |
| position | VARCHAR(10) | QB, RB, WR, etc. |
| jersey_number | INTEGER | Jersey number |
| height | VARCHAR(10) | e.g., "6-2" |
| weight | INTEGER | In pounds |
| college | VARCHAR(100) | College attended |

### games
| Column | Type | Description |
|--------|------|-------------|
| id | INTEGER PK | Auto-increment |
| season | INTEGER | e.g., 2025 |
| week | INTEGER | Week number |
| game_date | DATE | Game date |
| home_team_id | INTEGER FK | Reference to teams |
| away_team_id | INTEGER FK | Reference to teams |
| home_score | INTEGER | Final home score |
| away_score | INTEGER | Final away score |

### player_game_stats
| Column | Type | Description |
|--------|------|-------------|
| id | INTEGER PK | Auto-increment |
| player_id | INTEGER FK | Reference to players |
| game_id | INTEGER FK | Reference to games |
| pass_attempts | INTEGER | Passing attempts |
| pass_completions | INTEGER | Completions |
| pass_yards | INTEGER | Passing yards |
| pass_tds | INTEGER | Passing TDs |
| interceptions | INTEGER | INTs thrown |
| rush_attempts | INTEGER | Rushing attempts |
| rush_yards | INTEGER | Rushing yards |
| rush_tds | INTEGER | Rushing TDs |
| receptions | INTEGER | Receptions |
| receiving_yards | INTEGER | Receiving yards |
| receiving_tds | INTEGER | Receiving TDs |

---

## Entity Framework Core Models

```csharp
// Models/Team.cs
public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Conference { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    
    // Navigation properties
    public ICollection<Player> Players { get; set; } = new List<Player>();
    public ICollection<Game> HomeGames { get; set; } = new List<Game>();
    public ICollection<Game> AwayGames { get; set; } = new List<Game>();
}

// Models/Player.cs
public class Player
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public string Position { get; set; } = string.Empty;
    public int? JerseyNumber { get; set; }
    public string? Height { get; set; }
    public int? Weight { get; set; }
    public string? College { get; set; }
    
    // Navigation properties
    public Team? Team { get; set; }
    public ICollection<PlayerGameStats> GameStats { get; set; } = new List<PlayerGameStats>();
}

// Models/Game.cs
public class Game
{
    public int Id { get; set; }
    public int Season { get; set; }
    public int Week { get; set; }
    public DateTime GameDate { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    
    // Navigation properties
    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;
    public ICollection<PlayerGameStats> PlayerStats { get; set; } = new List<PlayerGameStats>();
}

// Models/PlayerGameStats.cs
public class PlayerGameStats
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public int GameId { get; set; }
    
    // Passing
    public int PassAttempts { get; set; }
    public int PassCompletions { get; set; }
    public int PassYards { get; set; }
    public int PassTouchdowns { get; set; }
    public int Interceptions { get; set; }
    
    // Rushing
    public int RushAttempts { get; set; }
    public int RushYards { get; set; }
    public int RushTouchdowns { get; set; }
    
    // Receiving
    public int Receptions { get; set; }
    public int ReceivingYards { get; set; }
    public int ReceivingTouchdowns { get; set; }
    
    // Navigation properties
    public Player Player { get; set; } = null!;
    public Game Game { get; set; } = null!;
}
```

```csharp
// Data/AppDbContext.cs
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<PlayerGameStats> PlayerGameStats => Set<PlayerGameStats>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
    }
}
```

---

## Repository Pattern & Multi-Database Support

The application uses the **Repository Pattern** to abstract data access, allowing seamless switching between SQLite, PostgreSQL, and SQL Server without modifying business logic.

### Supported Database Providers

| Provider | NuGet Package | Use Case |
|----------|---------------|----------|
| SQLite | `Microsoft.EntityFrameworkCore.Sqlite` | Local development, MVP |
| PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL` | Production (Linux/cloud) |
| SQL Server | `Microsoft.EntityFrameworkCore.SqlServer` | Production (Windows/Azure) |

### Repository Interfaces

```csharp
// Data/Repositories/IRepository.cs
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}

// Data/Repositories/ITeamRepository.cs
public interface ITeamRepository : IRepository<Team>
{
    Task<Team?> GetByAbbreviationAsync(string abbreviation);
    Task<IEnumerable<Team>> GetByConferenceAsync(string conference);
    Task UpsertAsync(Team team);
}

// Data/Repositories/IPlayerRepository.cs
public interface IPlayerRepository : IRepository<Player>
{
    Task<IEnumerable<Player>> GetByTeamAsync(int teamId);
    Task<Player?> GetByNameAsync(string name);
    Task UpsertAsync(Player player);
}

// Data/Repositories/IGameRepository.cs
public interface IGameRepository : IRepository<Game>
{
    Task<IEnumerable<Game>> GetBySeasonAsync(int season);
    Task<IEnumerable<Game>> GetByWeekAsync(int season, int week);
    Task UpsertAsync(Game game);
}

// Data/Repositories/IStatsRepository.cs
public interface IStatsRepository : IRepository<PlayerGameStats>
{
    Task<IEnumerable<PlayerGameStats>> GetPlayerStatsAsync(string playerName, int season);
    Task<IEnumerable<PlayerGameStats>> GetGameStatsAsync(int gameId);
    Task UpsertAsync(PlayerGameStats stats);
}
```

### Repository Implementation

```csharp
// Data/Repositories/TeamRepository.cs
public class TeamRepository : ITeamRepository
{
    private readonly AppDbContext _context;
    
    public TeamRepository(AppDbContext context)
    {
        _context = context;
    }
    
    public async Task<Team?> GetByIdAsync(int id)
        => await _context.Teams.FindAsync(id);
    
    public async Task<IEnumerable<Team>> GetAllAsync()
        => await _context.Teams.ToListAsync();
    
    public async Task<Team?> GetByAbbreviationAsync(string abbreviation)
        => await _context.Teams.FirstOrDefaultAsync(t => t.Abbreviation == abbreviation);
    
    public async Task UpsertAsync(Team team)
    {
        var existing = await GetByAbbreviationAsync(team.Abbreviation);
        if (existing != null)
        {
            existing.Name = team.Name;
            existing.City = team.City;
            existing.Conference = team.Conference;
            existing.Division = team.Division;
            _context.Teams.Update(existing);
        }
        else
        {
            await _context.Teams.AddAsync(team);
        }
        await _context.SaveChangesAsync();
    }
    
    // ... other implementations
}
```

### Database Provider Configuration

```csharp
// Extensions/ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebScraperServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Configure database based on provider setting
        var provider = configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        services.AddDbContext<AppDbContext>(options =>
        {
            switch (provider.ToLower())
            {
                case "sqlite":
                    options.UseSqlite(connectionString);
                    break;
                case "postgresql":
                    options.UseNpgsql(connectionString);
                    break;
                case "sqlserver":
                    options.UseSqlServer(connectionString);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported database provider: {provider}");
            }
        });
        
        // Register repositories
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IStatsRepository, StatsRepository>();
        
        // Register scraper services
        services.AddHttpClient<ITeamScraperService, TeamScraperService>();
        services.AddHttpClient<IPlayerScraperService, PlayerScraperService>();
        services.AddHttpClient<IGameScraperService, GameScraperService>();
        services.AddHttpClient<IStatsScraperService, StatsScraperService>();
        
        return services;
    }
}
```

### Configuration Examples

```json
// appsettings.json - SQLite (Development)
{
  "DatabaseProvider": "Sqlite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=data/nfl_data.db"
  }
}

// appsettings.Production.json - PostgreSQL
{
  "DatabaseProvider": "PostgreSQL",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=nfl_data;Username=user;Password=pass"
  }
}

// appsettings.Production.json - SQL Server
{
  "DatabaseProvider": "SqlServer",
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=NflData;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

---

## Implementation Phases

### Phase 1: Foundation
- [ ] Create .NET 8 Console App solution structure
- [ ] Add NuGet package dependencies
- [ ] Configure dependency injection and hosting
- [ ] Create EF Core models and DbContext
- [ ] Run initial EF migration (`dotnet ef migrations add InitialCreate`)
- [ ] Implement base scraper service with rate limiting
- [ ] Configure Serilog logging

### Phase 2: Repository & Data Access
- [ ] Implement generic `IRepository<T>` interface
- [ ] Create specialized repository interfaces (ITeamRepository, etc.)
- [ ] Implement repository classes with upsert logic
- [ ] Configure multi-database provider support in DI

### Phase 3: Core Scrapers
- [ ] Teams scraper - fetch all 32 NFL teams
- [ ] Players scraper - fetch active rosters
- [ ] Games scraper - fetch schedule and scores

### Phase 4: Stats Collection
- [ ] Player game stats scraper
- [ ] Data validation and cleaning
- [ ] Upsert logic for duplicate detection

### Phase 5: Polish
- [ ] CLI argument parsing with System.CommandLine
- [ ] Polly retry policies for transient failures
- [ ] Unit tests for HTML parsing logic
- [ ] Integration tests with test database

---

## Scraping Best Practices

1. **Rate Limiting:** 1-2 requests per second max
2. **User-Agent:** Use a descriptive, honest user agent
3. **Caching:** Cache responses to avoid redundant requests
4. **Error Handling:** Graceful failure with logging
5. **Respect robots.txt:** Check and honor directives

---

## Base Scraper Service Pattern

```csharp
// Services/Scrapers/BaseScraperService.cs
public abstract class BaseScraperService
{
    protected readonly HttpClient _httpClient;
    protected readonly ILogger _logger;
    protected readonly ScraperSettings _settings;
    
    protected BaseScraperService(
        HttpClient httpClient, 
        ILogger logger,
        IOptions<ScraperSettings> settings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
        
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_settings.UserAgent);
    }
    
    protected async Task<HtmlDocument?> FetchPageAsync(string url)
    {
        try
        {
            _logger.LogInformation("Fetching: {Url}", url);
            
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var html = await response.Content.ReadAsStringAsync();
            
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            
            // Rate limiting delay
            await Task.Delay(_settings.RequestDelayMs);
            
            return doc;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch {Url}", url);
            return null;
        }
    }
    
    protected abstract Task ScrapeAsync();
}
```

```csharp
// Services/Scrapers/TeamScraperService.cs
public class TeamScraperService : BaseScraperService, ITeamScraperService
{
    private readonly ITeamRepository _teamRepository;
    
    public TeamScraperService(
        HttpClient httpClient,
        ILogger<TeamScraperService> logger,
        IOptions<ScraperSettings> settings,
        ITeamRepository teamRepository) 
        : base(httpClient, logger, settings)
    {
        _teamRepository = teamRepository;
    }
    
    public async Task ScrapeTeamsAsync()
    {
        var doc = await FetchPageAsync("https://www.pro-football-reference.com/teams/");
        if (doc == null) return;
        
        var teamNodes = doc.DocumentNode.SelectNodes("//table[@id='teams_active']//tr");
        
        foreach (var node in teamNodes ?? Enumerable.Empty<HtmlNode>())
        {
            var team = ParseTeamNode(node);
            if (team != null)
            {
                await _teamRepository.UpsertAsync(team);
            }
        }
        
        _logger.LogInformation("Teams scrape complete");
    }
    
    private Team? ParseTeamNode(HtmlNode node)
    {
        // Parse HTML node into Team entity
        // Implementation depends on actual HTML structure
    }
}
```

---

## Example Usage (Target API)

```csharp
// Program.cs - Entry point with DI
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebScraper.Services.Scrapers;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddWebScraperServices(context.Configuration);
    })
    .Build();

var scraper = host.Services.GetRequiredService<INflScraperService>();

// Scrape all teams
await scraper.ScrapeTeamsAsync();

// Scrape current season games
await scraper.ScrapeGamesAsync(season: 2025);

// Scrape player stats for a specific week
await scraper.ScrapePlayerStatsAsync(season: 2025, week: 10);

// Query data via repository
var statsRepo = host.Services.GetRequiredService<IStatsRepository>();
var stats = await statsRepo.GetPlayerStatsAsync("Patrick Mahomes", season: 2025);
```

```csharp
// CLI argument handling example
dotnet run -- --scrape teams
dotnet run -- --scrape games --season 2025
dotnet run -- --scrape stats --season 2025 --week 10
```

---

## Configuration (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=data/nfl_data.db"
  },
  "ScraperSettings": {
    "RequestDelayMs": 1500,
    "MaxRetries": 3,
    "UserAgent": "NFLScraper/1.0 (educational project)",
    "TimeoutSeconds": 30
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "logs/scraper-.log", "rollingInterval": "Day" } }
    ]
  }
}
```

---

## Success Criteria (MVP)

- [ ] Successfully scrape and store all 32 NFL teams
- [ ] Scrape active player rosters for all teams
- [ ] Scrape game results for current/recent season
- [ ] Store player stats for at least 1 full week of games
- [ ] Query database and retrieve meaningful results

---

## Future Enhancements (Post-MVP)

- Add defensive and special teams stats
- Historical data backfill (multiple seasons)
- REST API layer for data access
- Dashboard/visualization frontend
- Automated weekly scheduling
- Data export to CSV/JSON
- Fantasy football scoring calculations

---

## Legal & Ethical Considerations

- This project is for **educational/personal use**
- Do not overload source servers
- Do not redistribute scraped data commercially
- Consider using official APIs where available (NFL has limited public APIs)
- Some sites actively block scrapers - respect their wishes

---

## NuGet Dependencies

```xml
<!-- WebScraper.csproj -->
<ItemGroup>
  <!-- HTML Parsing -->
  <PackageReference Include="HtmlAgilityPack" Version="1.11.*" />
  
  <!-- Entity Framework Core -->
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.*" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.*" />
  
  <!-- Database Providers (install based on target environment) -->
  <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.*" />
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.*" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.*" />
  
  <!-- Dependency Injection & Hosting -->
  <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.*" />
  <PackageReference Include="Microsoft.Extensions.Http" Version="8.0.*" />
  
  <!-- Logging -->
  <PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.*" />
  <PackageReference Include="Serilog.Sinks.Console" Version="5.0.*" />
  <PackageReference Include="Serilog.Sinks.File" Version="5.0.*" />
  
  <!-- CLI Parsing (optional) -->
  <PackageReference Include="System.CommandLine" Version="2.0.0-beta4.*" />
</ItemGroup>
```

### Install via CLI
```bash
# Core packages
dotnet add package HtmlAgilityPack
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Serilog.Extensions.Hosting

# Database providers (add based on needs)
dotnet add package Microsoft.EntityFrameworkCore.Sqlite      # For local dev
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL    # For PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.SqlServer  # For SQL Server
```

---

*Document Version: 1.0 | Last Updated: January 2026*
