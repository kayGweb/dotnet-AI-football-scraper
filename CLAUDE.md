# NFL Web Scraper - Project Guide

## Overview
Originally a .NET 8 Console application that scrapes NFL football data from multiple sources (Pro Football Reference, ESPN API, and more) and stores it in a structured database. As of the M0 refactor, the project is in the process of becoming a containerized microservice — a reusable `WebScraper.Core` class library backs the existing CLI and will back a new ASP.NET Core Web API (+ SignalR + Blazor Server admin) + MCP server for Claude consumption. The architecture supports pluggable data providers — switch between HTML scraping and REST API sources via configuration. See:
- `AGENT_MVP.md` — original design specification
- `API_INTEGRATION_PLAN.md` — multi-provider extension plan (on review branch)
- `CHATBOT_MICROSERVICE_PLAN.md` — **current** microservice transformation plan (milestones M0–M6)

## Tech Stack
- **Framework:** .NET 8 (class library + console app today; Web API + Blazor Server + MCP server coming in M1–M4)
- **HTML Parsing:** HtmlAgilityPack, AngleSharp
- **JSON Parsing:** System.Text.Json (built into .NET 8)
- **ORM:** Entity Framework Core 8
- **Database:** SQLite (dev default), PostgreSQL (production target on DigitalOcean), SQL Server (future Azure target)
- **DI:** Microsoft.Extensions.Hosting / DependencyInjection
- **Logging:** Serilog (Console + File sinks)
- **Resilience:** Polly v8 + Microsoft.Extensions.Http.Resilience (retry, circuit breaker, timeout)

## Project Structure (post-M0)
```
WebScraper.sln                          # Solution file — references Core, Cli, Core.Tests
src/
├── WebScraper.Core/                    # Class library: models, DbContext, repos, scrapers
│   ├── WebScraper.Core.csproj          # Library csproj (AssemblyName=WebScraper.Core, RootNamespace=WebScraper)
│   ├── Models/
│   │   ├── IAuditableEntity.cs         # M0: data lineage interface (DataSource/FetchedAt/RecordId + CreatedAt/UpdatedAt)
│   │   ├── ISoftDeletable.cs           # M0: soft-delete interface (IsDeleted/DeletedAt/DeletedBy/DeleteReason)
│   │   ├── ApiQueryLog.cs              # M0: observability log of every API consumer request
│   │   ├── Team.cs                         # NFL team entity — implements IAuditableEntity + ISoftDeletable
│   │   ├── Player.cs                       # Player entity (FK -> Team), EspnId field — implements IAuditableEntity + ISoftDeletable
│   │   ├── Game.cs                         # Game entity (FKs -> HomeTeam, AwayTeam, Venue), quarter scores, ESPN metadata — implements IAuditableEntity + ISoftDeletable
│   │   ├── PlayerGameStats.cs              # Per-game player stats — ~40 stat columns — implements IAuditableEntity + ISoftDeletable
│   │   ├── Venue.cs                        # Stadium/venue entity — implements IAuditableEntity + ISoftDeletable
│   │   ├── TeamGameStats.cs                # Team-level per-game aggregates — implements IAuditableEntity + ISoftDeletable
│   │   ├── Injury.cs                       # Player injury reports per game — implements IAuditableEntity + ISoftDeletable
│   │   ├── ApiLink.cs                      # Catalog of ESPN API endpoints — implements IAuditableEntity + ISoftDeletable
│   │   ├── ScrapeResult.cs                # Scraper operation result (Success, RecordsProcessed, Errors)
│   │   ├── ScraperSettings.cs             # Config POCO: scraper options + DataProvider + Providers dict
│   │   ├── DataProvider.cs                # Enum: ProFootballReference, Espn, SportsDataIo, MySportsFeeds, NflCom
│   │   └── ApiProviderSettings.cs         # Config POCO: BaseUrl, ApiKey, AuthType, headers per provider
│   ├── Data/
│   │   ├── AppDbContext.cs                # EF Core DbContext — 9 DbSets, global soft-delete query filters, registers interceptor
│   │   ├── AuditingSaveChangesInterceptor.cs # M0: stamps CreatedAt/UpdatedAt, converts hard deletes to soft deletes
│   │   └── Repositories/
│   │       ├── IRepository.cs             # Generic repository interface
│   │       ├── ITeamRepository.cs         # Team-specific repository interface
│   │       ├── IPlayerRepository.cs       # Player-specific repository interface
│   │       ├── IGameRepository.cs         # Game-specific repository interface
│   │       ├── IStatsRepository.cs        # Stats-specific repository interface
│   │       ├── IVenueRepository.cs        # Venue-specific repository interface
│   │       ├── ITeamGameStatsRepository.cs # Team game stats repository interface
│   │       ├── IInjuryRepository.cs       # Injury repository interface
│   │       ├── IApiLinkRepository.cs      # API link repository interface
│   │       ├── TeamRepository.cs          # Team repository implementation
│   │       ├── PlayerRepository.cs        # Player repository implementation
│   │       ├── GameRepository.cs          # Game repository implementation (includes Venue)
│   │       ├── StatsRepository.cs         # Stats repository implementation (~40 stat fields)
│   │       ├── VenueRepository.cs         # Venue repository implementation (upsert by EspnId)
│   │       ├── TeamGameStatsRepository.cs # Team game stats implementation (upsert by GameId+TeamId)
│   │       ├── InjuryRepository.cs        # Injury repository implementation (upsert by GameId+EspnAthleteId)
│   │       └── ApiLinkRepository.cs       # API link repository implementation (upsert by Url)
│   ├── Services/
│   │   ├── RateLimiterService.cs          # Global rate limiter (SemaphoreSlim-based)
│   │   ├── ConsoleDisplayService.cs       # User-facing console output (tables, banners, menus, progress) — CLI-specific but kept in Core for now
│   │   ├── DatabasePushService.cs         # Push local SQLite data to remote PostgreSQL
│   │   ├── DataProviderFactory.cs         # Maps DataProvider config to correct DI registrations
│   │   └── Scrapers/
│   │       ├── IScraperService.cs         # Scraper interfaces (ITeam/IPlayer/IGame/IStats)
│   │       ├── BaseScraperService.cs      # Abstract base for HTML: FetchPageAsync, rate limiting
│   │       ├── BaseApiService.cs          # Abstract base for JSON APIs: FetchJsonAsync<T>, auth, rate limiting
│   │       ├── TeamScraperService.cs      # PFR: Scrapes 32 NFL teams
│   │       ├── PlayerScraperService.cs    # PFR: Scrapes player rosters per team
│   │       ├── GameScraperService.cs      # PFR: Scrapes season schedules/scores
│   │       ├── StatsScraperService.cs     # PFR: Scrapes per-game player stats from box scores
│   │       ├── Espn/
│   │       │   ├── EspnDtos.cs            # DTO classes for ESPN JSON (teams, scoreboard, summary, gameInfo, injuries, links)
│   │       │   ├── EspnMappings.cs        # ESPN team ID ↔ NFL abbreviation + division lookup
│   │       │   ├── EspnTeamService.cs     # ESPN API: Scrapes teams via /teams endpoint
│   │       │   ├── EspnPlayerService.cs   # ESPN API: Scrapes rosters via /teams/{id}/roster
│   │       │   ├── EspnGameService.cs     # ESPN API: Scrapes scores, venues, quarter scores, API links via /scoreboard
│   │       │   └── EspnStatsService.cs    # ESPN API: Scrapes all 10 stat categories, team stats, venue, injuries, API links via /summary
│   │       ├── SportsDataIo/
│   │       │   ├── SportsDataDtos.cs      # DTO classes for SportsData.io JSON responses
│   │       │   ├── SportsDataTeamService.cs     # SportsData.io: Teams via /scores/json/Teams
│   │       │   ├── SportsDataPlayerService.cs   # SportsData.io: Players via /scores/json/Players/{team}
│   │       │   ├── SportsDataGameService.cs     # SportsData.io: Scores via /scores/json/ScoresByWeek
│   │       │   └── SportsDataStatsService.cs    # SportsData.io: Stats via /stats/json/PlayerGameStatsByWeek
│   │       ├── MySportsFeeds/
│   │       │   ├── MySportsFeedsDtos.cs          # DTO classes for MySportsFeeds JSON responses
│   │       │   ├── MySportsFeedsTeamService.cs   # MySportsFeeds: Teams via /{season}/teams.json
│   │       │   ├── MySportsFeedsPlayerService.cs # MySportsFeeds: Players via /players.json
│   │       │   ├── MySportsFeedsGameService.cs   # MySportsFeeds: Games via /{season}/games.json
│   │       │   └── MySportsFeedsStatsService.cs  # MySportsFeeds: Stats via /{season}/week/{week}/player_gamelogs.json
│   │       └── NflCom/
│   │           ├── NflComDtos.cs                 # DTO classes for NFL.com JSON responses
│   │           ├── NflComTeamService.cs          # NFL.com: Teams via /teams
│   │           ├── NflComPlayerService.cs        # NFL.com: Rosters via /teams/{abbr}/roster
│   │           ├── NflComGameService.cs          # NFL.com: Games via /games?season=&seasonType=REG&week=
│   │           └── NflComStatsService.cs         # NFL.com: Stats via /games/{gameDetailId}/stats
│   ├── Migrations/
│   │   ├── 20260304000000_InitialPostgres.cs     # Initial migration (Up/Down)
│   │   ├── 20260304000000_InitialPostgres.Designer.cs
│   │   ├── 20260309231025_ExpandedSchema.cs          # Expanded schema migration (4 new tables, ~40 new columns)
│   │   ├── 20260309231025_ExpandedSchema.Designer.cs
│   │   └── AppDbContextModelSnapshot.cs              # Current model snapshot
│   │   # NOTE: M0 adds lineage + soft-delete columns + ApiQueryLogs table. Run
│   │   # `dotnet ef migrations add AuditableAndSoftDelete --project src/WebScraper.Core --startup-project src/WebScraper.Cli`
│   │   # to generate the M0 migration before running the app — see CHATBOT_MICROSERVICE_PLAN.md M0.
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs # DI wiring: DB (with interceptor), repos, delegates to DataProviderFactory
└── WebScraper.Cli/                     # Console app (the existing CLI)
    ├── WebScraper.Cli.csproj           # Exe csproj, references WebScraper.Core, Serilog + Hosting packages
    ├── Program.cs                      # Entry point: CLI dispatch, interactive REPL, data display
    ├── appsettings.json                # Config: DB provider, data provider, scraper settings, Serilog
    └── Properties/AssemblyInfo.cs
data/                                   # SQLite database directory
tests/WebScraper.Core.Tests/            # xUnit test project (renamed from tests/WebScraper.Tests)
├── WebScraper.Core.Tests.csproj        # Test project — references src/WebScraper.Core/WebScraper.Core.csproj
├── Helpers/
│   └── TestDbContextFactory.cs         # In-memory SQLite factory for repository tests
├── Repositories/
│   ├── TeamRepositoryTests.cs          # 10 tests: CRUD, upsert, queries
│   ├── PlayerRepositoryTests.cs        # 6 tests: CRUD, FK relationships
│   ├── GameRepositoryTests.cs          # 5 tests: CRUD, season/week queries
│   └── StatsRepositoryTests.cs         # 4 tests: Upsert, player/game stats queries
├── Scrapers/
│   ├── TeamScraperParsingTests.cs      # 8 tests: PFR HTML parsing
│   ├── GameScraperParsingTests.cs      # 2 tests: PFR abbreviation mapping
│   ├── Espn/
│   │   ├── EspnMappingsTests.cs        # ESPN ID ↔ NFL abbreviation tests (all 32 teams)
│   │   ├── EspnTeamServiceTests.cs     # ESPN team scraping with mock HTTP
│   │   └── EspnGameServiceTests.cs     # ESPN scoreboard parsing with mock HTTP
│   ├── SportsDataIo/
│   │   ├── SportsDataTeamServiceTests.cs   # SportsData.io team scraping tests
│   │   └── SportsDataStatsServiceTests.cs  # SportsData.io stats DTO deserialization tests
│   ├── MySportsFeeds/
│   │   ├── MySportsFeedsTeamServiceTests.cs    # MySportsFeeds nested JSON parsing tests
│   │   └── MySportsFeedsPlayerServiceTests.cs  # MySportsFeeds player/stats DTO tests
│   └── NflCom/
│       └── NflComTeamServiceTests.cs   # NFL.com team scraping + graceful error handling
├── Services/
│   ├── BaseApiServiceTests.cs          # FetchJsonAsync, auth configuration (Header/Basic/None)
│   ├── DataProviderFactoryTests.cs     # Provider registration per provider string
│   └── ConsoleDisplayServiceTests.cs   # Banner, tables, menus, status output, provider validation
├── Configuration/
│   └── ProviderConfigTests.cs          # Config binding, provider settings, --source override
└── Models/
    ├── ModelTests.cs                   # 4 tests: Default values for all entities
    └── ScrapeResultTests.cs            # 5 tests: Default values, Succeeded/Failed factory methods
```

## Multi-Provider Architecture

The application supports multiple data sources through a provider abstraction layer:

```
Program.cs (CLI dispatch — same interfaces regardless of provider)
    ↓
ITeamScraperService / IPlayerScraperService / IGameScraperService / IStatsScraperService
    ↓                           ↓
BaseScraperService          BaseApiService
(HTML — PFR)               (JSON — ESPN, SportsData.io, etc.)
    ↓                           ↓
FetchPageAsync(url)         FetchJsonAsync<T>(url)
→ HtmlDocument              → T (deserialized)
    ↓                           ↓
Repository Layer (unchanged) ← UpsertAsync()
    ↓
AppDbContext → SQLite / PostgreSQL / SQL Server
```

### Data Providers
| Provider | Config Value | Auth | Status |
|----------|-------------|------|--------|
| Pro Football Reference | `ProFootballReference` | None (HTML scraping) | Implemented |
| ESPN API | `Espn` | None (open JSON API) | Implemented |
| SportsData.io | `SportsDataIo` | API key header | Implemented |
| MySportsFeeds | `MySportsFeeds` | HTTP Basic auth | Implemented |
| NFL.com | `NflCom` | None (undocumented) | Implemented |

### BaseApiService (`Services/Scrapers/BaseApiService.cs`)
Abstract base class for all JSON API providers, parallel to `BaseScraperService`:
- `FetchJsonAsync<T>(url)` — GET request, deserialize via `System.Text.Json`, rate limiting, error handling
- `ConfigureAuth()` — auto-applies auth based on `ApiProviderSettings.AuthType`:
  - `"None"` — no auth headers
  - `"Header"` — adds custom header (e.g., `Ocp-Apim-Subscription-Key` for SportsData.io)
  - `"Basic"` — HTTP Basic auth (e.g., MySportsFeeds)
- Injects `ApiProviderSettings` (per-provider config) and shared `RateLimiterService`

### DataProviderFactory (`Services/DataProviderFactory.cs`)
Static helper that maps the `DataProvider` config string to the correct set of DI registrations:
- `RegisterScrapers(services, settings)` — switches on provider name, registers the 4 scraper interfaces
- `AddScraperHttpClient<TInterface, TImpl>()` — registers HTML scrapers with Polly resilience
- `AddApiHttpClient<TInterface, TImpl>()` — registers API scrapers with `BaseAddress`, auth headers, and Polly resilience

### ESPN Provider (`Services/Scrapers/Espn/`)
| Service | Interface | ESPN Endpoint | Key Logic |
|---------|-----------|---------------|-----------|
| `EspnTeamService` | `ITeamScraperService` | `/teams` | Maps ESPN team IDs → NFL abbreviations via `EspnMappings` |
| `EspnPlayerService` | `IPlayerScraperService` | `/teams/{espnId}/roster` | Converts ESPN height (inches) to "X-Y" format |
| `EspnGameService` | `IGameScraperService` | `/scoreboard?dates={year}&week={n}&seasontype=2` | Stores ESPN event IDs, upserts venues, persists quarter scores & EspnEventId, stores API links |
| `EspnStatsService` | `IStatsScraperService` | `/summary?event={eventId}` | Parses all 10 boxscore categories (passing, rushing, receiving, fumbles, defensive, interceptions, kick returns, punt returns, kicking, punting); extracts team-level stats → TeamGameStats; venues from gameInfo; injuries; API links from header |
| `EspnDtos` | — | — | DTO classes for teams, scoreboard, summary, gameInfo, injuries, team statistics, linescores, header links |
| `EspnMappings` | — | — | Bidirectional ESPN ID ↔ NFL abbreviation map for all 32 teams + division lookup |

## Database Schema
Nine tables with the following relationships:
- **Teams** — 32 NFL teams (id, name, abbreviation, city, conference, division)
- **Players** — FK to Teams via `TeamId` (nullable for free agents); `EspnId` for ESPN athlete matching
- **Games** — Two FKs to Teams: `HomeTeamId`, `AwayTeamId` (both use `DeleteBehavior.Restrict`); optional FK to `Venues`; includes quarter scores (HomeQ1-Q4, HomeOT, AwayQ1-Q4, AwayOT), `EspnEventId`, `GameStatus`, `HomeWinner`, `Attendance`, `NeutralSite`
- **PlayerGameStats** — Composite FKs to `Players` and `Games`; ~40 stat columns across 10 categories: passing (C/A, yards, TD, INT, QBR, sacks), rushing (attempts, yards, TD, long), receiving (rec, yards, TD, targets, long, YPR), fumbles, defensive (tackles, sacks, TFL, PD, QBH), interceptions (caught, yards, TD), kick returns, punt returns, kicking (FG, XP, points), punting (punts, yards, avg, TB, inside20)
- **Venues** — Stadium info (EspnId UK, name, city, state, country, IsGrass, IsIndoor)
- **TeamGameStats** — Team-level per-game aggregates (FKs to Games+Teams, UK on GameId+TeamId); first downs, yards, efficiency, red zone, turnovers, penalties, possession time
- **Injuries** — Player injury reports per game (FKs to Games+Players, UK on GameId+EspnAthleteId); status, injury type, body location, return date
- **ApiLinks** — Discovered ESPN API endpoints (UK on Url); endpoint type, relation, season/week, ESPN event ID, timestamps
- **ApiQueryLogs** (M0) — Observability log of every public API consumer request: `Id` (long PK), `Timestamp`, `ApiKeyId`, `ApiKeyName`, `Method`, `Path`, `QueryString`, `StatusCode`, `DurationMs`, `ResponseBytes`, `UserAgent`, `CorrelationId`. Indexes on `Timestamp` and on `(ApiKeyId, Timestamp)` for dashboard queries. Populated asynchronously by `ApiQueryLoggingMiddleware` via a background `Channel<T>` writer in the M1 Web API — the hot path never blocks on the DB.

### Cross-cutting columns (M0)
Every non-log entity (Teams through ApiLinks) now implements `IAuditableEntity` + `ISoftDeletable` and gains 9 columns:
- **Data lineage (IAuditableEntity):** `DataSource`, `DataSourceFetchedAt`, `DataSourceRecordId`, `CreatedAt`, `UpdatedAt`
- **Soft delete (ISoftDeletable):** `IsDeleted`, `DeletedAt`, `DeletedBy`, `DeleteReason`

`AuditingSaveChangesInterceptor` stamps `CreatedAt`/`UpdatedAt` on insert/update and rewrites hard deletes into soft deletes (`EntityState.Modified` with `IsDeleted = true`, `DeletedAt = UtcNow`). `AppDbContext.OnModelCreating` adds a global query filter `e => !e.IsDeleted` on all 8 entities so normal queries automatically hide deleted rows — admin code uses `.IgnoreQueryFilters()` in the review UI.

## Key Patterns
- **Repository Pattern** with generic `IRepository<T>` base and specialized interfaces per entity
- **Upsert logic** — each repository has `UpsertAsync()` that checks for existing records before insert/update
- **Multi-database support** — provider selected via `DatabaseProvider` in `appsettings.json`
- **Multi-data-source support** — data provider selected via `ScraperSettings.DataProvider` in `appsettings.json`
- **Provider factory** — `DataProviderFactory` maps config to correct DI registrations; adding a new provider requires zero changes to interfaces, repositories, or Program.cs

## Configuration
`appsettings.json` sections:
- `DatabaseProvider` — `"Sqlite"` (default) | `"PostgreSQL"` | `"SqlServer"`
- `ConnectionStrings.DefaultConnection` — connection string for selected provider (SQLite by default)
- `ScraperSettings` — global scraper config:
  - `RequestDelayMs` (1500), `MaxRetries` (3), `UserAgent`, `TimeoutSeconds` (30)
  - `DataProvider` — `"ProFootballReference"` (default) | `"Espn"` | `"SportsDataIo"` | `"MySportsFeeds"` | `"NflCom"`
  - `Providers` — per-provider config dictionary with `BaseUrl`, `ApiKey`, `AuthType`, `AuthHeaderName`, `RequestDelayMs`, `CustomHeaders`
- `Serilog` — structured logging config

`appsettings.Local.json` (git-ignored, for secrets):
- `ConnectionStrings.PostgreSQL` — remote PostgreSQL connection string used by the `push` command

## Push to Server (SQLite -> PostgreSQL)

The app uses a two-database workflow: scrape data into a local SQLite database, then push to a remote PostgreSQL server on demand.

### Setup
1. Add your Neon (or other PostgreSQL) connection string to `WebScraper/appsettings.Local.json`:
   ```json
   {
     "ConnectionStrings": {
       "PostgreSQL": "Host=ep-xxx.neon.tech;Database=neondb;Username=neondb_owner;Password=REAL_PASSWORD;SSL Mode=Require"
     }
   }
   ```
2. This file is git-ignored (`appsettings.*.json` pattern in `.gitignore`).

### Usage
```bash
dotnet run -- push                     # CLI: push all local data to remote PostgreSQL
```
Or use option **5** ("Push to server") in the interactive menu.

### How It Works
- `DatabasePushService` opens a second `AppDbContext` pointed at the PostgreSQL connection string
- Runs EF Core migrations on the remote database (creates tables if they don't exist)
- Reads all data from the local SQLite context (teams, players, games, stats)
- Upserts each record into the remote database, resolving FK relationships by natural keys (abbreviation for teams, name+team for players, season+week+teams for games)
- Returns a `ScrapeResult` with counts and any errors

## Data Access Layer

### AppDbContext (`Data/AppDbContext.cs`)
- EF Core `DbContext` with `DbSet<>` for Teams, Players, Games, PlayerGameStats, Venues, TeamGameStats, Injuries, ApiLinks
- `OnModelCreating` configures:
 - `Game.HomeTeam` / `Game.AwayTeam` — two FKs to Team with `DeleteBehavior.Restrict`
 - `Game.Venue` — optional FK to Venue
 - `PlayerGameStats` — FKs to Player and Game
 - `Player.Team` — optional FK (`IsRequired(false)`)
 - `TeamGameStats` — FKs to Game and Team; unique index on `GameId + TeamId`
 - `Injury` — FK to Game; optional FK to Player; unique index on `GameId + EspnAthleteId`
 - `ApiLink` — optional FKs to Game and Team; unique index on `Url`
 - `Venue` — unique index on `EspnId`

### Repository Interfaces
| Interface | Lookup Methods | Upsert Key |
|-----------|---------------|------------|
| `ITeamRepository` | `GetByAbbreviationAsync`, `GetByConferenceAsync` | Abbreviation |
| `IPlayerRepository` | `GetByTeamAsync`, `GetByNameAsync` | Name + TeamId |
| `IGameRepository` | `GetBySeasonAsync`, `GetByWeekAsync` | Season + Week + HomeTeamId + AwayTeamId |
| `IStatsRepository` | `GetPlayerStatsAsync`, `GetGameStatsAsync` | PlayerId + GameId |
| `IVenueRepository` | `GetByEspnIdAsync` | EspnId |
| `ITeamGameStatsRepository` | `GetByGameAsync`, `GetByGameAndTeamAsync` | GameId + TeamId |
| `IInjuryRepository` | `GetByGameAsync`, `GetByGameAndAthleteAsync` | GameId + EspnAthleteId |
| `IApiLinkRepository` | `GetByUrlAsync`, `GetByGameAsync`, `GetByEndpointTypeAsync` | Url |

### Repository Implementations
All repositories follow the same pattern:
1. Full CRUD via `IRepository<T>` (GetById, GetAll, Add, Update, Delete, Exists)
2. Specialized query methods per interface
3. `UpsertAsync` — finds existing record by natural key, updates if found, inserts if not

## Scraper Services

### Architecture
- **BaseScraperService** — abstract base for HTML scrapers, injected with `HttpClient`, `ILogger`, `ScraperSettings`, `RateLimiterService`
  - `FetchPageAsync(url)` — fetches HTML, parses via HtmlAgilityPack, respects rate limits
- **BaseApiService** — abstract base for JSON API scrapers, injected with `HttpClient`, `ILogger`, `ApiProviderSettings`, `RateLimiterService`
  - `FetchJsonAsync<T>(url)` — fetches JSON, deserializes via System.Text.Json, handles auth, respects rate limits
- **RateLimiterService** — singleton, uses `SemaphoreSlim` to enforce `RequestDelayMs` between requests globally
- **ScrapeResult** — all scraper interface methods return `Task<ScrapeResult>` with `Success`, `RecordsProcessed`, `RecordsFailed`, `Message`, and `Errors` fields. Factory methods: `ScrapeResult.Succeeded(count, message)` and `ScrapeResult.Failed(message)`
- **ConsoleDisplayService** — singleton for user-facing console output (separate from Serilog). Provides `PrintBanner()`, `PrintScrapeResult()`, `PrintTeamsTable()`, `PrintGamesTable()` (auto-detects venue data for wider format), `PrintPlayersTable()`, `PrintStatsTable()` (groups by offense/defense/kicking/returns), `PrintVenuesTable()`, `PrintTeamGameStatsTable()`, `PrintInjuriesTable()`, `PrintDatabaseStatus()` (all 8 tables), interactive menu methods (`PrintMainMenu`, `PrintScrapeMenu`, `PrintViewMenu` with 8 options, `PrintSourceMenu`), and colored status output (`PrintError`, `PrintSuccess`, `PrintWarning`)

### PFR Scraper Details
| Service | Interface | Data Source URL | Key Parse Logic |
|---------|-----------|----------------|-----------------|
| `TeamScraperService` | `ITeamScraperService` | `/teams/` | Parses `teams_active` table; maps PFR abbreviations to NFL standard. Supports single-team scrape via `ScrapeTeamAsync(abbreviation)` |
| `PlayerScraperService` | `IPlayerScraperService` | `/teams/{abbr}/{year}_roster.htm` | Parses `roster` table; extracts name, position, jersey, height, weight, college |
| `GameScraperService` | `IGameScraperService` | `/years/{season}/games.htm` | Parses `games` table; determines home/away via `@` location marker |
| `StatsScraperService` | `IStatsScraperService` | `/boxscores/{date}0{home}.htm` | Parses `player_offense` table; extracts pass/rush/rec stats per player |

### PFR Abbreviation Mapping
Scrapers maintain a mapping between PFR team abbreviations (e.g., `kan`, `crd`, `rav`) and standard NFL abbreviations (e.g., `KC`, `ARI`, `BAL`). Defined in `TeamScraperService` and `GameScraperService`.

### ESPN API Details
| Service | Interface | ESPN Endpoint | Key Parse Logic |
|---------|-----------|---------------|-----------------|
| `EspnTeamService` | `ITeamScraperService` | `/teams` | Traverses `sports[0].leagues[0].teams[]`; maps ESPN IDs to NFL abbreviations |
| `EspnPlayerService` | `IPlayerScraperService` | `/teams/{espnId}/roster` | Iterates `athletes[].items[]`; converts height from inches to "X-Y" format |
| `EspnGameService` | `IGameScraperService` | `/scoreboard?dates=&week=&seasontype=2` | Parses events; upserts venues from `competition.venue`; extracts quarter scores from `linescores`; persists `EspnEventId`, `GameStatus`, `HomeWinner`, `Attendance`; caches event IDs; stores summary API links |
| `EspnStatsService` | `IStatsScraperService` | `/summary?event={eventId}` | Parses `boxscore.players[].statistics[]` across 10 categories (passing/rushing/receiving/fumbles/defensive/interceptions/kickReturns/puntReturns/kicking/punting); extracts `boxscore.teams[].statistics[]` → TeamGameStats; venue from `gameInfo`; injuries from `injuries[]`; API links from `header.links[]` |

### ESPN Team ID Mapping
`EspnMappings` provides bidirectional lookup between ESPN numeric IDs and NFL abbreviations for all 32 teams. Also includes conference/division lookup by NFL abbreviation.

### SportsData.io API Details
| Service | Interface | Endpoint | Key Parse Logic |
|---------|-----------|----------|-----------------|
| `SportsDataTeamService` | `ITeamScraperService` | `/scores/json/Teams` | Flat JSON array; uses standard NFL abbreviations — no mapping needed |
| `SportsDataPlayerService` | `IPlayerScraperService` | `/scores/json/Players/{team}` | Flat array per team; height provided as string |
| `SportsDataGameService` | `IGameScraperService` | `/scores/json/ScoresByWeek/{season}/{week}` | Standard abbreviations for home/away teams |
| `SportsDataStatsService` | `IStatsScraperService` | `/stats/json/PlayerGameStatsByWeek/{season}/{week}` | All player stats for entire week in one call; flat field mapping |

**Auth:** API key via `Ocp-Apim-Subscription-Key` header (configured in `appsettings.json`). Uses standard NFL abbreviations throughout — no team ID mapping required.

### MySportsFeeds API Details
| Service | Interface | Endpoint | Key Parse Logic |
|---------|-----------|----------|-----------------|
| `MySportsFeedsTeamService` | `ITeamScraperService` | `/{season}/teams.json` | Nested `teams[].team`; uses standard NFL abbreviations |
| `MySportsFeedsPlayerService` | `IPlayerScraperService` | `/players.json?team={abbr}&season={season}` | Nested `players[].player`; concatenates `firstName` + `lastName` |
| `MySportsFeedsGameService` | `IGameScraperService` | `/{season}/games.json?week={n}` | Nested `games[].schedule`; scores in `schedule.score` sub-object |
| `MySportsFeedsStatsService` | `IStatsScraperService` | `/{season}/week/{week}/player_gamelogs.json` | Deeply nested `gamelogs[].stats.passing/rushing/receiving` |

**Auth:** HTTP Basic with API key as username and `"MYSPORTSFEEDS"` as password (handled by `BaseApiService.ConfigureAuth()`). Uses standard NFL abbreviations.

### NFL.com API Details
| Service | Interface | Endpoint | Key Parse Logic |
|---------|-----------|----------|-----------------|
| `NflComTeamService` | `ITeamScraperService` | `/teams` | Flat `teams[]` array; standard NFL abbreviations |
| `NflComPlayerService` | `IPlayerScraperService` | `/teams/{abbr}/roster` | Flat `roster[]` array; jersey/weight as strings |
| `NflComGameService` | `IGameScraperService` | `/games?season={year}&seasonType=REG&week={n}` | Caches `gameDetailId` in-memory for stats lookups |
| `NflComStatsService` | `IStatsScraperService` | `/games/{gameDetailId}/stats` | `homeTeamStats`/`awayTeamStats` with nested passing/rushing/receiving |

**Auth:** None required. Endpoints are undocumented and may change — most fragile provider. Uses standard NFL abbreviations.

## DI & Program Entry Point

### ServiceCollectionExtensions (`src/WebScraper.Core/Extensions/ServiceCollectionExtensions.cs`)
- `AddWebScraperServices(IServiceCollection, IConfiguration)` extension method wires everything. It is called by every host (CLI today; API + MCP + Worker in later milestones) so there is exactly one composition root for Core:
  - Binds `ScraperSettings` from config
  - Registers `AuditingSaveChangesInterceptor` as singleton
  - Configures `AppDbContext` with provider from `DatabaseProvider` setting (SQLite/PostgreSQL/SqlServer) and attaches the auditing interceptor via the `(sp, options)` overload
  - Registers all 8 repositories as scoped services
  - Registers `RateLimiterService`, `ConsoleDisplayService` as singletons; `DatabasePushService` as scoped
  - Delegates scraper registration to `DataProviderFactory.RegisterScrapers()`

### Polly Resilience Policies
Each scraper's `HttpClient` (both HTML and API) is configured with a resilience pipeline:
- **Retry** — exponential backoff (2s, 4s, 8s), up to `MaxRetries` attempts on 408/429/5xx or network errors
- **Circuit Breaker** — opens after 70% failure rate over 30s (min 3 requests), breaks for 15s
- **Timeout** — per-attempt timeout from `ScraperSettings.TimeoutSeconds`

### Program.cs
- Uses `Host.CreateDefaultBuilder` with Serilog and `AddWebScraperServices`
- Pre-parses `--source` flag before host build to override `DataProvider` config via `AddInMemoryCollection`
- Applies pending migrations on startup via `MigrateAsync()`
- **Interactive mode** — launches with no args or `interactive` command; menu-driven REPL with scrape, view, status, and source-switching submenus. Changing source rebuilds the DI container.
- **CLI mode** — command dispatch with input validation (season 1920-current, week 1-22)
- **Data display** — `list teams/players/games/stats` and `status` commands query the database and display formatted tables
- **ScrapeResult handling** — all scraper calls return `ScrapeResult`; printed via `ConsoleDisplayService.PrintScrapeResult()`; exit code 0 for success, 1 for failure
- Extracted `BuildHost()` helper shared between CLI and interactive modes
- `--help` / `-h` flag for usage info

## Database Migrations
- Migration files live in `src/WebScraper.Core/Migrations/`
- `InitialPostgres` migration creates the original 4 tables (Teams, Players, Games, PlayerGameStats) with FKs and indexes
- `ExpandedSchema` migration adds 4 new tables (Venues, TeamGameStats, Injuries, ApiLinks), new columns to Games (VenueId, Attendance, quarter scores, EspnEventId, etc.), new columns to PlayerGameStats (~40 stat fields), and Player.EspnId
- **Pending (M0):** `AuditableAndSoftDelete` migration — adds data lineage columns (DataSource, DataSourceFetchedAt, DataSourceRecordId, CreatedAt, UpdatedAt) and soft-delete columns (IsDeleted, DeletedAt, DeletedBy, DeleteReason) to all 8 entities, plus the new `ApiQueryLogs` table with its indexes. Generate it with:
  ```bash
  dotnet ef migrations add AuditableAndSoftDelete \
      --project src/WebScraper.Core \
      --startup-project src/WebScraper.Cli
  ```
  This cannot be hand-written safely — run the command once the .NET SDK is available.
- `Program.cs` calls `db.Database.MigrateAsync()` on startup — auto-applies pending migrations
- To add a new migration: `dotnet ef migrations add <Name> --project src/WebScraper.Core --startup-project src/WebScraper.Cli`
- To apply manually: `dotnet ef database update --project src/WebScraper.Core --startup-project src/WebScraper.Cli`

## Build & Run
```bash
dotnet restore
dotnet build
dotnet run --project src/WebScraper.Cli
```

## CLI Commands
All `dotnet run` commands below must target the CLI project explicitly: `dotnet run --project src/WebScraper.Cli -- <args>`.

```bash
# Interactive mode (menu-driven REPL)
dotnet run --project src/WebScraper.Cli                                # Launch interactive mode (default)
dotnet run --project src/WebScraper.Cli -- interactive                 # Launch interactive mode (explicit)

# Scrape commands
dotnet run --project src/WebScraper.Cli -- teams                       # Scrape all 32 NFL teams
dotnet run --project src/WebScraper.Cli -- teams --team KC             # Scrape a single team by abbreviation
dotnet run --project src/WebScraper.Cli -- players                     # Scrape rosters for all teams
dotnet run --project src/WebScraper.Cli -- games --season 2025         # Scrape full season schedule/scores
dotnet run --project src/WebScraper.Cli -- games --season 2025 --week 1  # Scrape games for a specific week
dotnet run --project src/WebScraper.Cli -- stats --season 2025 --week 1  # Scrape player stats for a week
dotnet run --project src/WebScraper.Cli -- all --season 2025           # Run full pipeline (teams, players, games)
dotnet run --project src/WebScraper.Cli -- teams --source Espn         # Override data source at runtime

# Push local data to remote PostgreSQL
dotnet run --project src/WebScraper.Cli -- push                        # Push all SQLite data to Neon/PostgreSQL

# Data display commands
dotnet run --project src/WebScraper.Cli -- list teams                  # Show all teams in database
dotnet run --project src/WebScraper.Cli -- list teams --conference AFC # Show teams by conference
dotnet run --project src/WebScraper.Cli -- list players --team KC      # Show roster for a team
dotnet run --project src/WebScraper.Cli -- list games --season 2025    # Show games for a season
dotnet run --project src/WebScraper.Cli -- list games --season 2025 --week 1  # Show games for a week
dotnet run --project src/WebScraper.Cli -- list stats --season 2025 --week 1  # Show player stats
dotnet run --project src/WebScraper.Cli -- list stats --player "Patrick Mahomes" --season 2025
dotnet run --project src/WebScraper.Cli -- list venues                 # Show all venues in database
dotnet run --project src/WebScraper.Cli -- list teamstats --season 2025 --week 1
dotnet run --project src/WebScraper.Cli -- list injuries --season 2025 --week 1
dotnet run --project src/WebScraper.Cli -- status                      # Show database record counts (all 9 tables)
```

To switch data sources permanently, set `DataProvider` in `appsettings.json`. To switch at runtime, use `--source <provider>`. SportsData.io and MySportsFeeds require API keys configured in `Providers` section.

## Interactive Mode

When launched with no arguments (or `interactive`), the app enters a menu-driven REPL:

```
NFL Web Scraper v1.0
Source: ESPN API  |  Database: SQLite (data/nfl_data.db)
----------------------------------------------------

Main Menu
----------------------------------------
1. Scrape data
2. View data
3. Database status
4. Change source (current: ESPN API)
5. Push to server (SQLite -> PostgreSQL)
6. Exit
```

- **Scrape submenu** — all scrape operations (teams, single team, players, games, stats, full pipeline) with inline prompts for season/week/abbreviation
- **View submenu** — query and display database data using formatted tables (teams, players by team, games by season/week with venue/attendance, player stats grouped by offense/defense/kicking/returns, venues, team game stats, injuries)
- **Database status** — quick record counts for all tables
- **Change source** — switch between all 5 data providers at runtime; triggers host rebuild with new DI container
- **Push to server** — reads all data from local SQLite and upserts it into remote PostgreSQL (requires `ConnectionStrings:PostgreSQL` in `appsettings.Local.json`)
- **Input handling** — validates numeric input, handles EOF (Ctrl+D/Ctrl+Z) gracefully

## Testing
- **Framework:** xUnit with `Microsoft.NET.Test.Sdk`
- **Mocking:** Moq
- **Database:** In-memory SQLite via `TestDbContextFactory` helper
- **Project:** `tests/WebScraper.Core.Tests/` — references `src/WebScraper.Core/WebScraper.Core.csproj`
- **Run tests:** `dotnet test` from repo root, or `dotnet test tests/WebScraper.Core.Tests`

### Test Coverage
| Test File | Tests | What It Covers |
|-----------|-------|----------------|
| **Repositories** | | |
| `Repositories/TeamRepositoryTests.cs` | 10 | CRUD, GetByAbbreviation, GetByConference, Upsert insert/update, Delete, Exists |
| `Repositories/PlayerRepositoryTests.cs` | 6 | CRUD, GetByTeam, GetByName, Upsert insert/update, nullable TeamId |
| `Repositories/GameRepositoryTests.cs` | 5 | CRUD, GetBySeason, GetByWeek, Upsert insert/update with score changes |
| `Repositories/StatsRepositoryTests.cs` | 4 | Upsert insert/update, GetPlayerStats by name+season, GetGameStats |
| **PFR Scrapers** | | |
| `Scrapers/TeamScraperParsingTests.cs` | 8 | ParseTeamNode with valid HTML, header rows, missing links, ExtractCity, single-team ScrapeResult (match, not found, case-insensitive) |
| `Scrapers/GameScraperParsingTests.cs` | 2 | PFR-to-NFL abbreviation mapping (14 mapped + 4 unmapped pass-through) |
| **API Infrastructure** | | |
| `Services/BaseApiServiceTests.cs` | 10 | FetchJsonAsync deserialization (valid, malformed, error, null, case-insensitive), auth configuration (Header, Basic, None, missing key, custom headers) |
| `Services/DataProviderFactoryTests.cs` | 9 | All 5 providers register correctly, invalid provider throws, case-insensitive matching |
| `Configuration/ProviderConfigTests.cs` | 10 | Config binding from IConfiguration, default values, per-provider settings, API key handling, --source override, multi-provider dictionary |
| **ESPN Provider** | | |
| `Scrapers/Espn/EspnMappingsTests.cs` | 8 | All 32 ESPN IDs → NFL abbreviations, reverse mapping, division lookup, unknown IDs, case insensitivity |
| `Scrapers/Espn/EspnTeamServiceTests.cs` | 8 | JSON parsing, ESPN ID → NFL abbreviation mapping, conference/division, city, null response, single team, empty displayName; ScrapeResult assertions |
| `Scrapers/Espn/EspnGameServiceTests.cs` | 7 | Scoreboard parsing, home/away detection, score parsing, season/week, team not in DB, null response, no competitions; ScrapeResult assertions |
| **SportsData.io Provider** | | |
| `Scrapers/SportsDataIo/SportsDataTeamServiceTests.cs` | 6 | Flat JSON parsing, field mapping, single team, not found, empty name, null response; ScrapeResult assertions |
| `Scrapers/SportsDataIo/SportsDataStatsServiceTests.cs` | 6 | DTO deserialization, passing/rushing/receiving field mapping, zero stats, team/gameKey preservation |
| **MySportsFeeds Provider** | | |
| `Scrapers/MySportsFeeds/MySportsFeedsTeamServiceTests.cs` | 7 | Nested JSON parsing, field mapping, single team, not found, empty name, null conference defaults; ScrapeResult assertions |
| `Scrapers/MySportsFeeds/MySportsFeedsPlayerServiceTests.cs` | 10 | DTO deserialization, first/last name concatenation, all fields, currentTeam, nullable fields, empty names, gamelogs/stats deserialization |
| **NFL.com Provider** | | |
| `Scrapers/NflCom/NflComTeamServiceTests.cs` | 8 | JSON parsing, field mapping, single team, case-insensitive, not found, empty fullName, null response, unexpected JSON structure; ScrapeResult assertions |
| **UI Services** | | |
| `Services/ConsoleDisplayServiceTests.cs` | 21 | Banner output, ScrapeResult display (success/failure), table formatting (teams/players/games/stats), database status, error/success/warning output, interactive menus (main/scrape/view/source), provider validation and display names |
| **Models** | | |
| `Models/ModelTests.cs` | 4 | Default values for Team, Player, Game, PlayerGameStats, ScraperSettings |
| `Models/ScrapeResultTests.cs` | 5 | Default values, Succeeded factory (with count, with zero), Failed factory (with message, with error list) |

## Implementation Status

### Original MVP Phases
- [x] Phase 1: Project scaffolding (sln, gitignore, NuGet packages, appsettings, directory structure)
- [x] Phase 2: Domain models (Team, Player, Game, PlayerGameStats, ScraperSettings)
- [x] Phase 3: Data access layer (AppDbContext, repositories)
- [x] Phase 4: Scraper services
- [x] Phase 5: DI wiring & Program.cs
- [x] Phase 6: Database migrations
- [x] Phase 7: Polish (CLI args, Polly retry, validation)
- [x] Phase 8: Tests
- [x] Phase 9: Final verification

### API Integration Phases (see `API_INTEGRATION_PLAN.md`)
- [x] API Phase 1: Core provider infrastructure (DataProvider enum, ApiProviderSettings, BaseApiService)
- [x] API Phase 2: Provider factory & DI wiring (DataProviderFactory, updated ServiceCollectionExtensions)
- [x] API Phase 3: ESPN API provider (EspnTeamService, EspnPlayerService, EspnGameService, EspnStatsService, DTOs, mappings)
- [x] API Phase 4: SportsData.io API provider (SportsDataTeamService, SportsDataPlayerService, SportsDataGameService, SportsDataStatsService, DTOs)
- [x] API Phase 5: MySportsFeeds API provider (MySportsFeedsTeamService, MySportsFeedsPlayerService, MySportsFeedsGameService, MySportsFeedsStatsService, DTOs)
- [x] API Phase 6: NFL.com API provider (NflComTeamService, NflComPlayerService, NflComGameService, NflComStatsService, DTOs)
- [x] API Phase 7: CLI `--source` flag for runtime provider override
- [x] API Phase 8: Tests for API providers (BaseApiService, DataProviderFactory, EspnMappings, provider service tests, config binding)
- [x] API Phase 9: Documentation & polish

### User Interface Phases
- [x] UI Phase 1: ScrapeResult model + interface changes (all 20 scraper implementations return `Task<ScrapeResult>`)
- [x] UI Phase 2: ConsoleDisplayService + startup banner + early `--source` validation
- [x] UI Phase 3: Program.cs rewrite — ScrapeResult handling, exit codes, `RunAllAsync` pipeline
- [x] UI Phase 4: Data display commands (`list teams/players/games/stats`, `status`)
- [x] UI Phase 5: Interactive REPL mode (menu-driven scraping, viewing, source switching)
- [x] UI Phase 6: Test updates (ScrapeResult assertions on 6 test files, new ScrapeResultTests, ConsoleDisplayServiceTests)
- [x] UI Phase 7: CLAUDE.md documentation update

### Database Push Phase
- [x] Push Phase 1: DatabasePushService — reads local SQLite, upserts to remote PostgreSQL
- [x] Push Phase 2: CLI `push` command + interactive menu option (menu item 5)
- [x] Push Phase 3: Config split — SQLite as default, PostgreSQL connection string in git-ignored `appsettings.Local.json`

### ESPN Schema Expansion Phases
- [x] Schema Phase 1: New models — Venue, TeamGameStats, Injury, ApiLink
- [x] Schema Phase 2: Expanded models — Game (venue, attendance, quarter scores, EspnEventId), PlayerGameStats (~40 new stat columns), Team (nav props), Player (EspnId)
- [x] Schema Phase 3: AppDbContext — 4 new DbSets, FK relationships, unique indexes
- [x] Schema Phase 4: New repositories — Venue, TeamGameStats, Injury, ApiLink (interface + implementation)
- [x] Schema Phase 5: Updated StatsRepository.UpsertAsync for all new stat columns + GameRepository.UpsertAsync for expanded Game fields
- [x] Schema Phase 6: DI registration for new repositories
- [x] Schema Phase 7: Expanded EspnDtos — gameInfo, injuries, team statistics, linescores, header links
- [x] Schema Phase 8: EspnGameService — venue/attendance, EspnEventId, quarter scores, API links from scoreboard
- [x] Schema Phase 9: EspnStatsService — all 10 stat categories, team stats, venue, injuries, API links from /summary
- [x] Schema Phase 10: EF Core migration (ExpandedSchema)
- [x] Schema Phase 11: Console UI updates — PrintGamesTable (venue/attendance), PrintStatsTable (offense/defense/kicking/returns), PrintDatabaseStatus (all 8 tables), new PrintVenuesTable/PrintTeamGameStatsTable/PrintInjuriesTable, expanded View menu (8 options), new CLI list subcommands (venues, teamstats, injuries)

### Microservice Transformation Phases (see `CHATBOT_MICROSERVICE_PLAN.md`)
- [x] **M0 Phase 1:** Solution restructure — extract `src/WebScraper.Core` class library (AssemblyName=`WebScraper.Core`, RootNamespace=`WebScraper` so namespaces remain stable), rename existing console app to `src/WebScraper.Cli`, rename `tests/WebScraper.Tests` → `tests/WebScraper.Core.Tests` and retarget its project reference
- [x] **M0 Phase 2:** Cross-cutting interfaces — `IAuditableEntity` (DataSource/FetchedAt/RecordId + CreatedAt/UpdatedAt), `ISoftDeletable` (IsDeleted/DeletedAt/DeletedBy/DeleteReason)
- [x] **M0 Phase 3:** Implement interfaces on all 8 entities — Team, Player, Game, PlayerGameStats, Venue, TeamGameStats, Injury, ApiLink
- [x] **M0 Phase 4:** `AuditingSaveChangesInterceptor` — stamps CreatedAt/UpdatedAt, converts hard deletes to soft deletes; wired in DI via `AddDbContext((sp, options) => options.AddInterceptors(...))`
- [x] **M0 Phase 5:** Global query filters on all 8 entities in `AppDbContext.OnModelCreating` — deleted rows auto-hidden from normal queries
- [x] **M0 Phase 6:** `ApiQueryLog` entity + `DbSet<ApiQueryLog>` + indexes on `Timestamp` and `(ApiKeyId, Timestamp)` for the M1 observability dashboard
- [ ] **M0 Phase 7 (pending):** Generate EF Core migration `AuditableAndSoftDelete` — requires .NET SDK (`dotnet ef migrations add AuditableAndSoftDelete --project src/WebScraper.Core --startup-project src/WebScraper.Cli`)
- [ ] **M0 Phase 8 (pending):** Verify build + test suite pass on local machine (blocked in Claude environment — no .NET SDK)
- [ ] **M1:** ASP.NET Core Web API host (`src/WebScraper.Api`) — REST endpoints for teams/players/games/stats, API key auth, Problem Details, ApiQueryLoggingMiddleware
- [ ] **M2:** SignalR hub for real-time scrape progress broadcasts
- [ ] **M3:** Blazor Server admin dashboard — JWT auth, health, soft-delete review, ApiQueryLog viewer
- [ ] **M4:** MCP server host — exposes Core data as tools callable by Claude anywhere
- [ ] **M5:** Contract tests — recorded fixtures per provider
- [ ] **M6:** Docker + DigitalOcean App Platform deployment (PostgreSQL); future Azure App Service + MSSQL migration path

## Adding a New Data Provider
1. Create a folder: `Services/Scrapers/NewProvider/`
2. Create service classes implementing `ITeamScraperService`, `IPlayerScraperService`, `IGameScraperService`, `IStatsScraperService` — each extending `BaseApiService`
3. Create a DTOs file for the provider's JSON response shapes
4. Add config in `appsettings.json` under `Providers.NewProvider`
5. Add a case in `DataProviderFactory.RegisterScrapers()` for the new provider name
6. No changes needed to interfaces, repositories, models, or Program.cs
