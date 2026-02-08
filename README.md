# NFL Web Scraper

A .NET 8 console application that scrapes NFL football data from [Pro Football Reference](https://www.pro-football-reference.com/) and stores it in a structured relational database.

Collects teams, player rosters, game schedules/scores, and per-game player statistics (passing, rushing, receiving).

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
| `RequestDelayMs` | `1500` | Minimum delay between HTTP requests (milliseconds). Respects the data source's rate limits. |
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
| Scraper (Team) | 8 | HTML parsing, header row handling, city extraction, single-team scrape |
| Scraper (Game) | 2 | PFR-to-NFL abbreviation mapping |
| Models | 4 | Default property values |
| **Total** | **39** | |

## Project Structure

```
WebScraper.sln
WebScraper/
├── Program.cs                          # Entry point and CLI dispatch
├── appsettings.json                    # Configuration
├── Models/                             # Entity classes
├── Data/
│   ├── AppDbContext.cs                 # EF Core DbContext
│   └── Repositories/                   # Repository pattern implementations
├── Services/
│   ├── RateLimiterService.cs           # Global request rate limiter
│   └── Scrapers/                       # Scraper service implementations
├── Migrations/                         # EF Core migration files
└── Extensions/
    └── ServiceCollectionExtensions.cs  # DI registration
data/                                   # SQLite database directory
tests/WebScraper.Tests/                 # xUnit test project
```

## License

This project is for educational purposes.
