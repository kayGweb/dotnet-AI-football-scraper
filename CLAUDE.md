# NFL Web Scraper - Project Guide

## Overview
A .NET 8 Console application that scrapes NFL football data from public sources (Pro Football Reference, ESPN, NFL.com) and stores it in a structured database. See `AGENT_MVP.md` for full design specification.

## Tech Stack
- **Framework:** .NET 8 Console App
- **HTML Parsing:** HtmlAgilityPack, AngleSharp
- **ORM:** Entity Framework Core 8
- **Database:** SQLite (dev default), PostgreSQL, SQL Server (swappable via config)
- **DI:** Microsoft.Extensions.Hosting / DependencyInjection
- **Logging:** Serilog (Console + File sinks)
- **Resilience:** Polly

## Project Structure
```
WebScraper.sln                          # Solution file
WebScraper/
├── WebScraper.csproj                   # Project file with all NuGet refs
├── Program.cs                          # Entry point with CLI command dispatch
├── appsettings.json                    # Config: DB provider, scraper settings, Serilog
├── Models/
│   ├── Team.cs                         # NFL team entity
│   ├── Player.cs                       # Player entity (FK -> Team)
│   ├── Game.cs                         # Game entity (FKs -> HomeTeam, AwayTeam)
│   ├── PlayerGameStats.cs              # Per-game player stats (FKs -> Player, Game)
│   └── ScraperSettings.cs             # Config POCO for scraper options
├── Data/
│   ├── AppDbContext.cs                 # EF Core DbContext
│   └── Repositories/
│       ├── IRepository.cs             # Generic repository interface
│       ├── ITeamRepository.cs         # Team-specific repository interface
│       ├── IPlayerRepository.cs       # Player-specific repository interface
│       ├── IGameRepository.cs         # Game-specific repository interface
│       ├── IStatsRepository.cs        # Stats-specific repository interface
│       ├── TeamRepository.cs          # Team repository implementation
│       ├── PlayerRepository.cs        # Player repository implementation
│       ├── GameRepository.cs          # Game repository implementation
│       └── StatsRepository.cs         # Stats repository implementation
├── Services/
│   ├── RateLimiterService.cs          # Global rate limiter (SemaphoreSlim-based)
│   └── Scrapers/
│       ├── IScraperService.cs         # Scraper interfaces (ITeam/IPlayer/IGame/IStats)
│       ├── BaseScraperService.cs      # Abstract base: FetchPageAsync, rate limiting, logging
│       ├── TeamScraperService.cs      # Scrapes 32 NFL teams from PFR
│       ├── PlayerScraperService.cs    # Scrapes player rosters per team from PFR
│       ├── GameScraperService.cs      # Scrapes season schedules/scores from PFR
│       └── StatsScraperService.cs     # Scrapes per-game player stats from PFR box scores
└── Extensions/
    └── ServiceCollectionExtensions.cs # DI wiring: DB, repos, scrapers, HttpClient
data/                                   # SQLite database directory
tests/WebScraper.Tests/                 # (Phase 8)
```

## Database Schema
Four tables with the following relationships:
- **teams** — 32 NFL teams (id, name, abbreviation, city, conference, division)
- **players** — FK to teams via `TeamId` (nullable for free agents)
- **games** — Two FKs to teams: `HomeTeamId`, `AwayTeamId` (both use `DeleteBehavior.Restrict`)
- **player_game_stats** — Composite FKs to `players` and `games`; columns for passing/rushing/receiving stats

## Key Patterns
- **Repository Pattern** with generic `IRepository<T>` base and specialized interfaces per entity
- **Upsert logic** — each repository has `UpsertAsync()` that checks for existing records before insert/update
- **Multi-database support** — provider selected via `DatabaseProvider` in `appsettings.json`

## Configuration
`appsettings.json` sections:
- `DatabaseProvider` — `"Sqlite"` | `"PostgreSQL"` | `"SqlServer"`
- `ConnectionStrings.DefaultConnection` — connection string for selected provider
- `ScraperSettings` — `RequestDelayMs` (1500), `MaxRetries` (3), `UserAgent`, `TimeoutSeconds` (30)
- `Serilog` — structured logging config

## Data Access Layer

### AppDbContext (`Data/AppDbContext.cs`)
- EF Core `DbContext` with `DbSet<>` for Teams, Players, Games, PlayerGameStats
- `OnModelCreating` configures:
  - `Game.HomeTeam` / `Game.AwayTeam` — two FKs to Team with `DeleteBehavior.Restrict`
  - `PlayerGameStats` — FKs to Player and Game
  - `Player.Team` — optional FK (`IsRequired(false)`)

### Repository Interfaces
| Interface | Lookup Methods | Upsert Key |
|-----------|---------------|------------|
| `ITeamRepository` | `GetByAbbreviationAsync`, `GetByConferenceAsync` | Abbreviation |
| `IPlayerRepository` | `GetByTeamAsync`, `GetByNameAsync` | Name + TeamId |
| `IGameRepository` | `GetBySeasonAsync`, `GetByWeekAsync` | Season + Week + HomeTeamId + AwayTeamId |
| `IStatsRepository` | `GetPlayerStatsAsync`, `GetGameStatsAsync` | PlayerId + GameId |

### Repository Implementations
All repositories follow the same pattern:
1. Full CRUD via `IRepository<T>` (GetById, GetAll, Add, Update, Delete, Exists)
2. Specialized query methods per interface
3. `UpsertAsync` — finds existing record by natural key, updates if found, inserts if not

## Scraper Services

### Architecture
- **BaseScraperService** — abstract base class injected with `HttpClient`, `ILogger`, `ScraperSettings`, `RateLimiterService`
  - `FetchPageAsync(url)` — fetches HTML, parses via HtmlAgilityPack, respects rate limits
- **RateLimiterService** — singleton, uses `SemaphoreSlim` to enforce `RequestDelayMs` between requests globally
- All scrapers target **Pro Football Reference** (pro-football-reference.com)

### Scraper Details
| Service | Interface | Data Source URL | Key Parse Logic |
|---------|-----------|----------------|-----------------|
| `TeamScraperService` | `ITeamScraperService` | `/teams/` | Parses `teams_active` table; maps PFR abbreviations to NFL standard |
| `PlayerScraperService` | `IPlayerScraperService` | `/teams/{abbr}/{year}_roster.htm` | Parses `roster` table; extracts name, position, jersey, height, weight, college |
| `GameScraperService` | `IGameScraperService` | `/years/{season}/games.htm` | Parses `games` table; determines home/away via `@` location marker |
| `StatsScraperService` | `IStatsScraperService` | `/boxscores/{date}0{home}.htm` | Parses `player_offense` table; extracts pass/rush/rec stats per player |

### PFR Abbreviation Mapping
Scrapers maintain a mapping between PFR team abbreviations (e.g., `kan`, `crd`, `rav`) and standard NFL abbreviations (e.g., `KC`, `ARI`, `BAL`). Defined in `TeamScraperService` and `GameScraperService`.

## DI & Program Entry Point

### ServiceCollectionExtensions (`Extensions/ServiceCollectionExtensions.cs`)
- `AddWebScraperServices(IServiceCollection, IConfiguration)` extension method wires everything:
  - Binds `ScraperSettings` from config
  - Configures `AppDbContext` with provider from `DatabaseProvider` setting (SQLite/PostgreSQL/SqlServer)
  - Registers repositories as scoped services
  - Registers `RateLimiterService` as singleton
  - Registers scrapers via `AddHttpClient<TInterface, TImpl>()` for typed `HttpClient` injection

### Program.cs
- Uses `Host.CreateDefaultBuilder` with Serilog and `AddWebScraperServices`
- Auto-creates database on startup via `EnsureCreatedAsync()`
- CLI command dispatch via positional args

## Build & Run
```bash
dotnet restore
dotnet build
dotnet run --project WebScraper
```

## CLI Commands
```bash
dotnet run -- teams                            # Scrape all 32 NFL teams
dotnet run -- players                          # Scrape rosters for all teams
dotnet run -- games --season 2025              # Scrape full season schedule/scores
dotnet run -- games --season 2025 --week 1     # Scrape games for a specific week
dotnet run -- stats --season 2025 --week 1     # Scrape player stats for a week
dotnet run -- all --season 2025                # Run full pipeline (teams, players, games)
```

## Implementation Status
- [x] Phase 1: Project scaffolding (sln, gitignore, NuGet packages, appsettings, directory structure)
- [x] Phase 2: Domain models (Team, Player, Game, PlayerGameStats, ScraperSettings)
- [x] Phase 3: Data access layer (AppDbContext, repositories)
- [x] Phase 4: Scraper services
- [x] Phase 5: DI wiring & Program.cs
- [ ] Phase 6: Database migrations
- [ ] Phase 7: Polish (CLI args, Polly retry, validation)
- [ ] Phase 8: Tests
- [ ] Phase 9: Final verification
