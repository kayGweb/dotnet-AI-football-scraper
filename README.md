# NFL Web Scraper

A .NET 8 console application that scrapes NFL football data from multiple sources and stores it in a structured relational database. Supports five pluggable data providers — switch between HTML scraping and REST API sources via configuration or a CLI flag.

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
dotnet run --project WebScraper -- --help
```

The database is created automatically on first run. EF Core migrations are applied at startup, so there is no manual database setup required.

## Usage

```
dotnet run --project WebScraper -- <command> [options]
```

### Commands

| Command | Required Options | Description |
|---------|-----------------|-------------|
| `teams` | — | Scrape all 32 NFL teams |
| `teams` | `--team <abbr>` | Scrape a single team by NFL abbreviation |
| `players` | — | Scrape rosters for all teams (teams must be scraped first) |
| `games` | `--season <year>` | Scrape full season schedule and scores |
| `games` | `--season <year> --week <n>` | Scrape games for a specific week |
| `stats` | `--season <year> --week <n>` | Scrape per-game player statistics for a week |
| `all` | `--season <year>` | Run the full pipeline: teams, players, then games |

### Options

| Flag | Value | Description |
|------|-------|-------------|
| `--team` | NFL abbreviation | Team abbreviation (e.g., `KC`, `NE`, `DAL`) — used with `teams` command |
| `--season` | `1920` – current year | NFL season year |
| `--week` | `1` – `22` | Week number (regular season + playoffs) |
| `--source` | Provider name | Override the data provider at runtime (e.g., `Espn`, `SportsDataIo`, `MySportsFeeds`, `NflCom`) |
| `--help`, `-h` | — | Show help message |

### Examples

```bash
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
```

### Recommended Scrape Order

If running commands individually, follow this order to satisfy foreign key dependencies:

1. `teams` — populates the teams table
2. `players` — needs teams to exist for roster association
3. `games --season <year>` — needs teams for home/away references
4. `stats --season <year> --week <n>` — needs both players and games

The `all` command handles steps 1-3 automatically.

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
    "DataProvider": "ProFootballReference"
  }
}
```

Set `DataProvider` to change the default data source. Supported values: `ProFootballReference`, `Espn`, `SportsDataIo`, `MySportsFeeds`, `NflCom`. This can also be overridden at runtime with the `--source` flag without changing the config file.

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
| PFR Scraper (Game) | 2 | PFR-to-NFL abbreviation mapping |
| API Infrastructure | 10 | FetchJsonAsync deserialization, auth config (Header/Basic/None), custom headers |
| DataProviderFactory | 9 | All 5 providers register correctly, invalid provider throws, case-insensitive |
| Provider Config | 10 | Config binding, API keys, `--source` override, multi-provider dictionary |
| ESPN Mappings | 8 | All 32 ESPN ID mappings, reverse lookup, division lookup, case insensitivity |
| ESPN Services | 15 | Team JSON parsing, ID mapping, scoreboard parsing, home/away, scores |
| SportsData.io | 12 | Flat JSON parsing, DTO deserialization, passing/rushing/receiving stats |
| MySportsFeeds | 17 | Nested JSON parsing, name concatenation, gamelogs, nullable fields |
| NFL.com | 8 | JSON parsing, case-insensitive matching, graceful error handling |
| Models | 4 | Default property values |
| **Total** | **128** | |

## Architecture

The application uses a provider abstraction layer — all data providers implement the same four interfaces, so the CLI, repositories, and database layer are completely provider-agnostic:

```
Program.cs (CLI dispatch)
    ↓
ITeamScraperService / IPlayerScraperService / IGameScraperService / IStatsScraperService
    ↓                           ↓
BaseScraperService          BaseApiService
(HTML — PFR)               (JSON — ESPN, SportsData.io, etc.)
    ↓                           ↓
Repository Layer (unchanged) ← UpsertAsync()
    ↓
AppDbContext → SQLite / PostgreSQL / SQL Server
```

Adding a new provider requires no changes to interfaces, repositories, models, or the CLI. See `CLAUDE.md` for detailed instructions.

## Project Structure

```
WebScraper.sln
WebScraper/
├── Program.cs                          # Entry point, CLI dispatch, --source override
├── appsettings.json                    # Configuration (DB, providers, Serilog)
├── Models/                             # Entity classes + config POCOs
├── Data/
│   ├── AppDbContext.cs                 # EF Core DbContext
│   └── Repositories/                   # Repository pattern implementations
├── Services/
│   ├── RateLimiterService.cs           # Global request rate limiter
│   ├── DataProviderFactory.cs          # Maps provider config to DI registrations
│   └── Scrapers/
│       ├── BaseScraperService.cs       # Abstract base for HTML scraping
│       ├── BaseApiService.cs           # Abstract base for JSON APIs
│       ├── TeamScraperService.cs       # Pro Football Reference (HTML)
│       ├── PlayerScraperService.cs     # Pro Football Reference (HTML)
│       ├── GameScraperService.cs       # Pro Football Reference (HTML)
│       ├── StatsScraperService.cs      # Pro Football Reference (HTML)
│       ├── Espn/                       # ESPN API provider (4 services + DTOs + mappings)
│       ├── SportsDataIo/              # SportsData.io API provider (4 services + DTOs)
│       ├── MySportsFeeds/             # MySportsFeeds API provider (4 services + DTOs)
│       └── NflCom/                    # NFL.com API provider (4 services + DTOs)
├── Migrations/                         # EF Core migration files
└── Extensions/
    └── ServiceCollectionExtensions.cs  # DI wiring
data/                                   # SQLite database directory
tests/WebScraper.Tests/                 # xUnit test project (128 tests)
```

## License

This project is for educational purposes.
