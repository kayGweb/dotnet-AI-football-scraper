# NFL Web Scraper

A .NET 8 microservice that scrapes NFL football data from multiple sources and exposes it through a REST API, a Blazor Server admin dashboard, and an MCP server for Claude integration. Supports five pluggable data providers — switch between HTML scraping and REST API sources via configuration. Includes a standalone CLI mode and an interactive menu-driven REPL.

Collects teams, player rosters, game schedules/scores (including quarter scores, venues, attendance), per-game player statistics (10 categories: passing, rushing, receiving, fumbles, defensive, interceptions, kick returns, punt returns, kicking, punting), team-level aggregates, and injury reports.

### Components

| Component | Project | Description |
|-----------|---------|-------------|
| **REST API** | `WebScraper.Api` | Read-only endpoints + admin write endpoints (JWT/API key auth) |
| **Admin Dashboard** | `WebScraper.Api` | Blazor Server UI at `/admin/*` (MudBlazor dark theme) |
| **MCP Server** | `WebScraper.Mcp` | Claude-callable tools over the API (stdio transport) |
| **CLI** | `WebScraper.Cli` | Command-line scraper + interactive REPL |
| **Core Library** | `WebScraper.Core` | Shared models, DbContext, repositories, scrapers |

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

## Quick Start

### 1. Clone and build

```bash
git clone https://github.com/kayGweb/dotnet-AI-football-scraper.git
cd dotnet-AI-football-scraper
dotnet restore
dotnet build
```

### 2. Create a local secrets file (one-time setup)

Create `src/WebScraper.Api/appsettings.Local.json` (git-ignored) with your admin credentials and JWT signing key:

```json
{
  "Jwt": {
    "SigningKey": "GENERATE_WITH_openssl_rand_-base64_48_AT_LEAST_32_CHARS"
  },
  "InitialAdmin": {
    "Email": "admin@example.com",
    "Password": "YourSecurePassword123!"
  }
}
```

Generate a signing key: `openssl rand -base64 48`

The initial admin account is only created when the user table is empty — after first boot, manage users via the dashboard at `/admin/users`.

### 3. Start the API + Dashboard

```bash
dotnet run --project src/WebScraper.Api
```

The API starts at **http://localhost:5080**. On startup it automatically:
- Applies all pending EF Core migrations (creates the database if needed)
- Seeds Admin/Operator/Viewer roles
- Creates the initial admin user (if configured and user table is empty)

### 4. Access the application

| URL | What |
|-----|------|
| http://localhost:5080/admin | Admin dashboard login |
| http://localhost:5080/swagger | Swagger UI (Development mode only) |
| http://localhost:5080/api/v1/status | API status endpoint (requires API key) |
| http://localhost:5080/health | Health check |

Log in to the dashboard with the email/password from your `appsettings.Local.json`.

### 5. (Optional) Run the CLI

The CLI shares the same database and can be used alongside the API:

```bash
dotnet run --project src/WebScraper.Cli                              # Interactive mode
dotnet run --project src/WebScraper.Cli -- teams --source Espn       # Scrape teams via ESPN
dotnet run --project src/WebScraper.Cli -- status                    # Show database counts
```

### 6. (Optional) Set up the MCP Server for Claude

Build the MCP server and wire it to Claude Code or Claude Desktop:

```bash
dotnet build src/WebScraper.Mcp
```

Add to your Claude Code MCP config (`.mcp.json` or `settings.json`):

```json
{
  "mcpServers": {
    "nfl": {
      "command": "dotnet",
      "args": ["run", "--project", "src/WebScraper.Mcp", "--no-build"],
      "env": {
        "NFL_API_URL": "http://localhost:5080",
        "NFL_API_KEY": "your-api-key-here"
      }
    }
  }
}
```

The API must be running for the MCP server to work. Create an API key via the admin dashboard at `/admin/api-keys`.

## Admin Dashboard

The dashboard at `/admin/*` provides a visual interface for managing the entire system:

| Page | Path | Access | Description |
|------|------|--------|-------------|
| Login | `/admin/login` | Public | Email/password login form |
| Dashboard | `/admin` | All roles | Entity counts, recent jobs, system health |
| Jobs | `/admin/jobs` | All roles | Live job table (auto-refreshes every 5s) with status filter |
| New Scrape | `/admin/scrapes/new` | Admin, Operator | Trigger scrapes — select type, season, week |
| API Keys | `/admin/api-keys` | Admin | Create/revoke API keys (plaintext shown once on create) |
| Users | `/admin/users` | Admin | Create users, assign roles (Admin/Operator/Viewer) |
| Deleted Items | `/admin/deleted-items` | Admin | Review and restore soft-deleted records |
| API Usage | `/admin/api-usage` | All roles | Request charts, response codes, top endpoints/consumers |

### Authentication

Three auth schemes coexist on the same host:

| Scheme | Used by | How |
|--------|---------|-----|
| Cookie (`AdminCookie`) | Dashboard pages | Login form at `/admin/login` sets an 8-hour HttpOnly cookie |
| JWT Bearer | REST API write endpoints | `POST /api/v1/auth/login` returns a token; pass as `Authorization: Bearer <token>` |
| API Key | REST API read endpoints | Pass as `X-Api-Key: <plaintext>` header |

### Roles

| Role | Can do |
|------|--------|
| Admin | Everything — user/key management, soft-delete restore, push, scraping |
| Operator | Trigger scrapes, view jobs |
| Viewer | Read-only dashboard access |

## REST API

All read endpoints are under `/api/v1/` and require an `X-Api-Key` header with `read` scope. Write endpoints require a JWT with the appropriate role.

### Read Endpoints (API Key)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/teams` | Paged team list, optional `?conference=AFC\|NFC` |
| GET | `/api/v1/teams/{id}` | Single team by PK |
| GET | `/api/v1/teams/by-abbreviation/{abbr}` | Single team by NFL abbreviation |
| GET | `/api/v1/players` | Paged player list, optional `?teamId=`, `?position=` |
| GET | `/api/v1/players/{id}` | Single player |
| GET | `/api/v1/players/{id}/stats` | Player game stats, optional `?season=`, `?week=` |
| GET | `/api/v1/games` | Paged game list, optional `?season=`, `?week=`, `?teamId=` |
| GET | `/api/v1/games/{id}` | Single game with teams, venue, quarter scores |
| GET | `/api/v1/games/{id}/team-stats` | Team-level aggregates for a game |
| GET | `/api/v1/games/{id}/player-stats` | All player stat lines for a game |
| GET | `/api/v1/games/{id}/injuries` | Injury reports for a game |
| GET | `/api/v1/venues` | Paged venue list, optional `?state=`, `?isIndoor=` |
| GET | `/api/v1/venues/{id}` | Single venue |
| GET | `/api/v1/status` | Entity counts + freshest update timestamp |

### Admin Endpoints (JWT)

| Method | Route | Role | Description |
|--------|-------|------|-------------|
| POST | `/api/v1/auth/login` | — | Exchange email/password for JWT |
| GET | `/api/v1/auth/me` | Viewer | Current user profile + roles |
| POST | `/api/v1/auth/users` | Admin | Create user |
| GET | `/api/v1/auth/users` | Admin | List all users |
| GET/POST/DELETE | `/api/v1/api-keys` | Admin | API key management |
| GET | `/api/v1/deleted-items` | Admin | List soft-deleted items |
| POST | `/api/v1/deleted-items/{type}/{id}/restore` | Admin | Restore soft-deleted item |
| POST | `/api/v1/push` | Admin | Push SQLite data to PostgreSQL |
| POST | `/api/v1/scrape/{type}` | Operator | Trigger scrape (returns 202 + job ID) |
| GET | `/api/v1/jobs` | Operator | List scrape jobs |
| GET | `/api/v1/jobs/{id}` | Operator | Single job status |
| GET | `/api/v1/events` | Viewer | Replay scrape events (for SignalR catch-up) |

### Pagination

List endpoints accept `?page=` (default 1) and `?pageSize=` (default 25, max 200). Responses include `X-Total-Count` header.

### Rate Limiting

60 requests per minute per API key/user/IP. Returns `429 Too Many Requests` with `Retry-After` header when exceeded.

### Health Checks

| Endpoint | Purpose |
|----------|---------|
| `/health/live` | Process is up (no dependency checks) |
| `/health/ready` | DB is reachable |
| `/health` | All checks |

## CLI Mode

The CLI is a standalone scraper that shares the same database as the API.

```bash
# Interactive mode (menu-driven REPL)
dotnet run --project src/WebScraper.Cli

# Scrape commands
dotnet run --project src/WebScraper.Cli -- teams
dotnet run --project src/WebScraper.Cli -- teams --team KC
dotnet run --project src/WebScraper.Cli -- players
dotnet run --project src/WebScraper.Cli -- games --season 2025
dotnet run --project src/WebScraper.Cli -- games --season 2025 --week 1
dotnet run --project src/WebScraper.Cli -- stats --season 2025 --week 1
dotnet run --project src/WebScraper.Cli -- all --season 2025

# Override data source
dotnet run --project src/WebScraper.Cli -- teams --source Espn

# View data
dotnet run --project src/WebScraper.Cli -- list teams
dotnet run --project src/WebScraper.Cli -- list players --team KC
dotnet run --project src/WebScraper.Cli -- list games --season 2025 --week 1
dotnet run --project src/WebScraper.Cli -- status

# Push local SQLite to remote PostgreSQL
dotnet run --project src/WebScraper.Cli -- push
```

### Recommended Scrape Order

If running commands individually, follow this order to satisfy foreign key dependencies:

1. `teams` — populates the teams table
2. `players` — needs teams to exist for roster association
3. `games --season <year>` — needs teams for home/away references
4. `stats --season <year> --week <n>` — needs both players and games

The `all` command handles steps 1-3 automatically.

## Configuration

Settings live in `src/WebScraper.Api/appsettings.json` (API) and `src/WebScraper.Cli/appsettings.json` (CLI). Secrets go in the git-ignored `appsettings.Local.json`.

### Database Provider

```json
{
  "DatabaseProvider": "Sqlite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=data/nfl_data.db"
  }
}
```

Supported: `Sqlite` (default), `PostgreSQL`, `SqlServer`.

### Data Provider

```json
{
  "ScraperSettings": {
    "DataProvider": "Espn"
  }
}
```

Supported: `ProFootballReference`, `Espn`, `SportsDataIo`, `MySportsFeeds`, `NflCom`.

### API Authentication (appsettings.Local.json)

```json
{
  "Jwt": {
    "SigningKey": "your-secret-key-at-least-32-chars-long"
  },
  "InitialAdmin": {
    "Email": "admin@example.com",
    "Password": "SecurePassword123!"
  },
  "ApiKeys": {
    "Keys": [
      {
        "Id": "local-dev",
        "Name": "Local Development",
        "HashedKey": "sha256-hex-of-your-plaintext-key",
        "Scopes": ["read"]
      }
    ]
  }
}
```

Generate an API key hash: `echo -n 'your-plaintext-key' | sha256sum`

Once you can log in to the dashboard, create DB-backed API keys at `/admin/api-keys` and remove the bootstrap key from config.

### Push to PostgreSQL

To push local SQLite data to a remote PostgreSQL instance, add the connection string to `appsettings.Local.json`:

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=your-host;Database=nfl;Username=user;Password=pass;SSL Mode=Require"
  }
}
```

Then use the CLI (`dotnet run --project src/WebScraper.Cli -- push`) or the API (`POST /api/v1/push` with Admin JWT).

## Database

EF Core with code-first migrations. The database is created and migrated automatically at startup for both the API and CLI.

### Schema (12 tables)

| Table | Description |
|-------|-------------|
| Teams | 32 NFL teams (name, abbreviation, city, conference, division) |
| Players | Rosters with position, jersey, physical attributes, college, EspnId |
| Games | Schedules with quarter scores, venues, attendance, ESPN metadata |
| PlayerGameStats | Per-game stats across 10 categories (~40 stat columns) |
| Venues | Stadiums (name, city, state, grass/indoor) |
| TeamGameStats | Team-level per-game aggregates |
| Injuries | Player injury reports per game |
| ApiLinks | Discovered ESPN API endpoints |
| ApiKeys | DB-backed API keys (SHA-256 hashed) |
| ApiQueryLogs | Observability log of every API request |
| ScrapeJobs | Persistent scrape job queue with status tracking |
| ScrapeEvents | Transactional outbox for real-time scrape notifications |

All domain entities (Teams through ApiKeys) support soft delete and data lineage tracking.

## Testing

```bash
dotnet test                                    # Run all 220 tests
dotnet test --verbosity normal                 # Verbose output
dotnet test tests/WebScraper.Core.Tests        # Core tests only
```

## Project Structure

```
WebScraper.sln
src/
├── WebScraper.Core/          # Shared library: models, DbContext, repos, scrapers
├── WebScraper.Cli/           # Console app: CLI + interactive REPL
├── WebScraper.Api/           # Web API + Blazor admin dashboard
│   ├── Auth/                 # Identity, JWT, API key, cookie auth
│   ├── Controllers/          # REST endpoints (13 controllers)
│   ├── Components/           # Blazor Server pages (MudBlazor)
│   │   ├── Layout/           # AdminLayout (dark theme), EmptyLayout (login)
│   │   └── Pages/Admin/      # 8 dashboard pages + 2 dialog components
│   ├── Hubs/                 # SignalR hub for real-time scrape events
│   ├── Middleware/            # Query logging, rate limiting
│   └── Services/             # Job queue, event relay, API key management
└── WebScraper.Mcp/           # MCP server: 14 Claude-callable tools
tests/
└── WebScraper.Core.Tests/    # 220 xUnit tests
```

## License

This project is for educational purposes.
