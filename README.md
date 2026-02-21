# NFL Web Scraper

A .NET 8 console application that scrapes NFL football data from multiple sources and stores it in a structured relational database. Supports five pluggable data providers — switch between HTML scraping and REST API sources via configuration or a CLI flag. Includes both a CLI mode and an interactive menu-driven REPL.

Collects teams, player rosters, game schedules/scores, and per-game player statistics (passing, rushing, receiving).

### Data Providers

| Provider | Config Value | Auth | Description |
|----------|-------------|------|-------------|
| Pro Football Reference | `ProFootballReference` | None | HTML scraping (default) |
| ESPN API | `Espn` | None | Open JSON API |
| SportsData.io | `SportsDataIo` | API key header | Requires free/paid API key |
| MySportsFeeds | `MySportsFeeds` | HTTP Basic | Requires API key |
| NFL.com | `NflCom` | None | Undocumented JSON endpoints |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (8.0 or later)
- Git

No external database server is required — the application defaults to SQLite, which requires no installation.

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/kayGweb/dotnet-AI-football-scraper.git
cd dotnet-AI-football-scraper
```

### 2. Restore dependencies

```bash
dotnet restore
```

### 3. Build the application

```bash
dotnet build
```

### 4. Run the application

```bash
# Launch interactive mode (default)
dotnet run --project WebScraper

# Or use CLI mode
dotnet run --project WebScraper -- --help
```

The database is created automatically on first run. EF Core migrations are applied at startup, so there is no manual database setup required.

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
5. Exit
```

### Main Menu

| Option | Description |
|--------|-------------|
| **1. Scrape data** | Opens a submenu with all scrape operations (teams, single team, players, games by season, games by week, stats, full pipeline) |
| **2. View data** | Opens a submenu to query and display database contents in formatted tables |
| **3. Database status** | Shows record counts for all tables (teams, players, games, stat lines) |
| **4. Change source** | Switch between all 5 data providers at runtime — rebuilds the DI container |
| **5. Exit** | Exit the application |

### Scrape Submenu

| Option | Description |
|--------|-------------|
| 1 | Scrape all 32 NFL teams |
| 2 | Scrape a single team by abbreviation |
| 3 | Scrape all player rosters |
| 4 | Scrape games for a full season |
| 5 | Scrape games for a specific week |
| 6 | Scrape player stats for a specific week |
| 7 | Run the full pipeline (teams + players + games) |
| 8 | Back to main menu |

### View Submenu

| Option | Description |
|--------|-------------|
| 1 | View all teams |
| 2 | View players (all, or filtered by team) |
| 3 | View games (by season, optionally filtered by week) |
| 4 | View player stats (by player name + season, or by season + week) |
| 5 | Back to main menu |

### Source Switching

Selecting option 4 from the main menu displays all available providers with the current selection marked. Choosing a different provider rebuilds the application's dependency injection container with the new provider's services — no restart required.

## CLI Mode

```
dotnet run --project WebScraper -- <command> [options]
```

### Scrape Commands

| Command | Required Options | Description |
|---------|-----------------|-------------|
| `teams` | — | Scrape all 32 NFL teams |
| `teams` | `--team <abbr>` | Scrape a single team by NFL abbreviation |
| `players` | — | Scrape rosters for all teams (teams must be scraped first) |
| `games` | `--season <year>` | Scrape full season schedule and scores |
| `games` | `--season <year> --week <n>` | Scrape games for a specific week |
| `stats` | `--season <year> --week <n>` | Scrape per-game player statistics for a week |
| `all` | `--season <year>` | Run the full pipeline: teams, players, then games |

### View Commands

| Command | Required Options | Description |
|---------|-----------------|-------------|
| `list teams` | — | Show all teams in the database |
| `list teams` | `--conference <AFC\|NFC>` | Show teams filtered by conference |
| `list players` | — | Show all players |
| `list players` | `--team <abbr>` | Show roster for a specific team |
| `list games` | `--season <year>` | Show all games for a season |
| `list games` | `--season <year> --week <n>` | Show games for a specific week |
| `list stats` | `--season <year> --week <n>` | Show player stats for a week |
| `list stats` | `--player <name> --season <year>` | Show a player's season stats |
| `status` | — | Show database record counts |

### Options

| Flag | Value | Description |
|------|-------|-------------|
| `--team` | NFL abbreviation | Team abbreviation (e.g., `KC`, `NE`, `DAL`) — used with `teams` and `list players` |
| `--season` | `1920` – current year | NFL season year |
| `--week` | `1` – `22` | Week number (regular season + playoffs) |
| `--conference` | `AFC` or `NFC` | Conference filter for `list teams` |
| `--player` | Player name | Player name for `list stats` (e.g., `"Patrick Mahomes"`) |
| `--source` | Provider name | Override the data provider at runtime (e.g., `Espn`, `SportsDataIo`, `MySportsFeeds`, `NflCom`) |
| `--help`, `-h` | — | Show help message |

### Examples

```bash
# Launch interactive mode (default)
dotnet run --project WebScraper

# Scrape all 32 NFL teams
dotnet run --project WebScraper -- teams

# Scrape a single team by abbreviation
dotnet run --project WebScraper -- teams --team KC

# Scrape player rosters (requires teams to exist in DB)
dotnet run --project WebScraper -- players

# Scrape all games for the 2025 season
dotnet run --project WebScraper -- games --season 2025

# Scrape games for week 1 of 2025
dotnet run --project WebScraper -- games --season 2025 --week 1

# Scrape player stats for week 1 of 2025 (requires games to exist in DB)
dotnet run --project WebScraper -- stats --season 2025 --week 1

# Run the full pipeline (teams + players + games)
dotnet run --project WebScraper -- all --season 2025

# Use the ESPN API instead of the default provider
dotnet run --project WebScraper -- teams --source Espn

# Use SportsData.io for games (requires API key in appsettings.json)
dotnet run --project WebScraper -- games --season 2025 --source SportsDataIo

# View all teams in the database
dotnet run --project WebScraper -- list teams

# View AFC teams only
dotnet run --project WebScraper -- list teams --conference AFC

# View Kansas City Chiefs roster
dotnet run --project WebScraper -- list players --team KC

# View all games for the 2025 season
dotnet run --project WebScraper -- list games --season 2025

# View games for a specific week
dotnet run --project WebScraper -- list games --season 2025 --week 1

# View player stats for a week
dotnet run --project WebScraper -- list stats --season 2025 --week 1

# View an individual player's season stats
dotnet run --project WebScraper -- list stats --player "Patrick Mahomes" --season 2025

# Show database record counts
dotnet run --project WebScraper -- status
```

### Recommended Scrape Order

If running commands individually, follow this order to satisfy foreign key dependencies:

1. `teams` — populates the teams table
2. `players` — needs teams to exist for roster association
3. `games --season <year>` — needs teams for home/away references
4. `stats --season <year> --week <n>` — needs both players and games

The `all` command handles steps 1-3 automatically.

## Console Output

The application uses `ConsoleDisplayService` for all user-facing output, separate from Serilog's structured logging:

- **Startup banner** — shows the active data provider, database type, and connection info
- **Formatted tables** — teams, players, games, and stats are displayed in aligned, bordered tables
- **Scrape results** — each operation reports success/failure with record counts (e.g., `[SUCCESS] Teams: 32 records processed`)
- **Colored status messages** — errors (red), warnings (yellow), success (green), and info (cyan)
- **Interactive menus** — numbered menu options with the current provider highlighted

## Configuration

All settings are in `WebScraper/appsettings.json`.

### Database Provider

```json
{
  "DatabaseProvider": "Sqlite"
}
```

Supported values: `Sqlite`, `PostgreSQL`, `SqlServer`

### Connection String

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=data/nfl_data.db"
  }
}
```

Update the connection string to match your chosen provider:

| Provider | Example Connection String |
|----------|--------------------------|
| SQLite | `Data Source=data/nfl_data.db` |
| PostgreSQL | `Host=localhost;Database=nfl_data;Username=postgres;Password=yourpassword` |
| SQL Server | `Server=localhost;Database=nfl_data;Trusted_Connection=True;TrustServerCertificate=True` |

### Data Provider

```json
{
  "ScraperSettings": {
    "DataProvider": "Espn"
  }
}
```

Set `DataProvider` to change the default data source. Supported values: `ProFootballReference`, `Espn`, `SportsDataIo`, `MySportsFeeds`, `NflCom`. This can also be overridden at runtime with the `--source` flag (CLI mode) or through the source-switching menu (interactive mode) without changing the config file.

### Provider-Specific Settings

Each API provider has its own configuration block under `Providers`:

```json
{
  "ScraperSettings": {
    "Providers": {
      "Espn": {
        "BaseUrl": "https://site.api.espn.com/apis/site/v2/sports/football/nfl",
        "AuthType": "None",
        "RequestDelayMs": 1000
      },
      "SportsDataIo": {
        "BaseUrl": "https://api.sportsdata.io/v3/nfl",
        "ApiKey": "YOUR_API_KEY_HERE",
        "AuthType": "Header",
        "AuthHeaderName": "Ocp-Apim-Subscription-Key",
        "RequestDelayMs": 1000
      },
      "MySportsFeeds": {
        "BaseUrl": "https://api.mysportsfeeds.com/v2.1/pull/nfl",
        "ApiKey": "YOUR_API_KEY_HERE",
        "AuthType": "Basic",
        "RequestDelayMs": 1500
      },
      "NflCom": {
        "BaseUrl": "https://site.api.nfl.com/v1",
        "AuthType": "None",
        "RequestDelayMs": 1500
      }
    }
  }
}
```

| Setting | Description |
|---------|-------------|
| `BaseUrl` | Base URL for the provider's API |
| `ApiKey` | API key (required for SportsData.io and MySportsFeeds) |
| `AuthType` | Authentication method: `None`, `Header` (API key header), or `Basic` (HTTP Basic) |
| `AuthHeaderName` | Custom header name for API key (used when `AuthType` is `Header`) |
| `RequestDelayMs` | Per-provider rate limit override (milliseconds between requests) |
| `CustomHeaders` | Optional dictionary of additional HTTP headers |

To use **SportsData.io** or **MySportsFeeds**, add your API key to the respective `ApiKey` field in `appsettings.json`. ESPN and NFL.com require no API key.

### Scraper Settings

```json
{
  "ScraperSettings": {
    "RequestDelayMs": 1500,
    "MaxRetries": 3,
    "UserAgent": "NFLScraper/1.0 (educational project)",
    "TimeoutSeconds": 30
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `RequestDelayMs` | `1500` | Global minimum delay between HTTP requests (milliseconds). Individual providers can override this. |
| `MaxRetries` | `3` | Number of retry attempts on transient failures (408, 429, 5xx). Uses exponential backoff (2s, 4s, 8s). |
| `UserAgent` | `NFLScraper/1.0 (educational project)` | HTTP User-Agent header sent with every request. |
| `TimeoutSeconds` | `30` | Per-request timeout in seconds. |

### Logging

Logging is configured via Serilog with two output sinks:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/scraper-.log",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

- **Console** — real-time output during scraping
- **File** — daily rolling log files written to `logs/`

To increase verbosity (e.g., for debugging), change `"Default"` to `"Debug"`.

## Resilience

Each scraper's HTTP client is configured with a Polly v8 resilience pipeline:

- **Retry** — exponential backoff (2s, 4s, 8s) on 408/429/5xx responses and network errors
- **Circuit Breaker** — opens after 70% failure rate over 30 seconds (minimum 3 requests), pauses for 15 seconds
- **Timeout** — per-attempt timeout based on `TimeoutSeconds` setting

## Database

The application uses Entity Framework Core with code-first migrations. The database is created and migrated automatically at startup.

### Schema

| Table | Description |
|-------|-------------|
| `Teams` | 32 NFL teams with name, abbreviation, city, conference, division |
| `Players` | Player rosters with position, jersey number, physical attributes, college |
| `Games` | Season schedules with home/away teams, dates, and scores |
| `PlayerGameStats` | Per-game player statistics: passing, rushing, and receiving |

### Manual Migration Commands

```bash
# Apply pending migrations
dotnet ef database update --project WebScraper

# Add a new migration after model changes
dotnet ef migrations add <MigrationName> --project WebScraper
```

## Testing

The test suite uses xUnit with in-memory SQLite databases and Moq for mocking.

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --verbosity normal
```

### Test Coverage

| Area | Tests | Scope |
|------|-------|-------|
| Repository (Team) | 10 | CRUD, lookup by abbreviation/conference, upsert, delete, exists |
| Repository (Player) | 6 | CRUD, lookup by team/name, upsert, nullable team FK |
| Repository (Game) | 5 | CRUD, lookup by season/week, upsert with score updates |
| Repository (Stats) | 4 | Upsert, lookup by player name + season, lookup by game |
| PFR Scraper (Team) | 8 | HTML parsing, header row handling, city extraction, single-team scrape |
| PFR Scraper (Game) | 2 | PFR-to-NFL abbreviation mapping (14 mapped + 4 unmapped) |
| API Infrastructure | 10 | FetchJsonAsync deserialization, auth config (Header/Basic/None), custom headers |
| DataProviderFactory | 9 | All 5 providers register correctly, invalid provider throws, case-insensitive |
| Provider Config | 10 | Config binding, API keys, `--source` override, multi-provider dictionary |
| ESPN Mappings | 8 | All 32 ESPN ID mappings, reverse lookup, division lookup, case insensitivity |
| ESPN Services | 15 | Team JSON parsing, ID mapping, scoreboard parsing, home/away, scores |
| SportsData.io | 12 | Flat JSON parsing, DTO deserialization, passing/rushing/receiving stats |
| MySportsFeeds | 17 | Nested JSON parsing, name concatenation, gamelogs, nullable fields |
| NFL.com | 8 | JSON parsing, case-insensitive matching, graceful error handling |
| Console Display | 21 | Banner, tables, menus, scrape results, status, colored output, provider validation |
| Models | 5 | Default property values for all entities and ScraperSettings |
| ScrapeResult | 5 | Default values, Succeeded/Failed factory methods |
| **Total** | **220** | |

## Architecture

The application uses a provider abstraction layer — all data providers implement the same four interfaces, so the CLI, repositories, and database layer are completely provider-agnostic:

```
Program.cs (CLI dispatch + Interactive REPL)
    ↓
ConsoleDisplayService (banner, tables, menus, colored output)
    ↓
ITeamScraperService / IPlayerScraperService / IGameScraperService / IStatsScraperService
    ↓                           ↓
BaseScraperService          BaseApiService
(HTML — PFR)               (JSON — ESPN, SportsData.io, etc.)
    ↓                           ↓
ScrapeResult (structured success/failure with record counts)
    ↓
Repository Layer (unchanged) ← UpsertAsync()
    ↓
AppDbContext → SQLite / PostgreSQL / SQL Server
```

Adding a new provider requires no changes to interfaces, repositories, models, or the CLI. See `CLAUDE.md` for detailed instructions.

## Project Structure

```
WebScraper.sln
WebScraper/
├── Program.cs                          # Entry point: CLI dispatch, interactive REPL, data display
├── appsettings.json                    # Configuration (DB, providers, Serilog)
├── Models/
│   ├── Team.cs                        # NFL team entity
│   ├── Player.cs                      # Player entity (FK → Team)
│   ├── Game.cs                        # Game entity (FKs → HomeTeam, AwayTeam)
│   ├── PlayerGameStats.cs             # Per-game player stats (FKs → Player, Game)
│   ├── ScrapeResult.cs                # Structured scraper result (Success, RecordsProcessed, Errors)
│   ├── ScraperSettings.cs             # Config POCO for scraper options
│   ├── DataProvider.cs                # Enum for supported data providers
│   └── ApiProviderSettings.cs         # Per-provider config POCO (BaseUrl, ApiKey, AuthType)
├── Data/
│   ├── AppDbContext.cs                # EF Core DbContext
│   └── Repositories/                  # Repository pattern implementations
│       ├── IRepository.cs             # Generic repository interface
│       ├── ITeamRepository.cs         # Team-specific queries
│       ├── IPlayerRepository.cs       # Player-specific queries
│       ├── IGameRepository.cs         # Game-specific queries
│       ├── IStatsRepository.cs        # Stats-specific queries
│       └── (implementations)          # TeamRepository, PlayerRepository, GameRepository, StatsRepository
├── Services/
│   ├── RateLimiterService.cs          # Global request rate limiter (SemaphoreSlim)
│   ├── ConsoleDisplayService.cs       # User-facing console output (tables, menus, banners, colored status)
│   ├── DataProviderFactory.cs         # Maps provider config → DI registrations
│   └── Scrapers/
│       ├── IScraperService.cs         # Scraper interfaces (ITeam/IPlayer/IGame/IStats)
│       ├── BaseScraperService.cs      # Abstract base for HTML scraping (PFR)
│       ├── BaseApiService.cs          # Abstract base for JSON APIs (auth, rate limiting)
│       ├── TeamScraperService.cs      # Pro Football Reference: teams
│       ├── PlayerScraperService.cs    # Pro Football Reference: rosters
│       ├── GameScraperService.cs      # Pro Football Reference: schedules/scores
│       ├── StatsScraperService.cs     # Pro Football Reference: player stats
│       ├── Espn/                      # ESPN API provider (4 services + DTOs + mappings)
│       ├── SportsDataIo/             # SportsData.io API provider (4 services + DTOs)
│       ├── MySportsFeeds/            # MySportsFeeds API provider (4 services + DTOs)
│       └── NflCom/                   # NFL.com API provider (4 services + DTOs)
├── Migrations/                        # EF Core migration files
└── Extensions/
    └── ServiceCollectionExtensions.cs # DI wiring
data/                                  # SQLite database directory
tests/WebScraper.Tests/                # xUnit test project (220 tests)
```

## License

This project is for educational purposes.
