# NFL Web Scraper

A .NET 8 agent-managed NFL data platform that scrapes football data from multiple sources and exposes it through a REST API, a Blazor Server admin dashboard, and an MCP server for Claude integration. Supports five pluggable data providers — switch between HTML scraping and REST API sources via configuration. Includes a standalone CLI mode and an interactive menu-driven REPL.

**Data collected:** teams, franchises, team-seasons, player rosters (keyed on `EspnId`), game schedules/scores (preseason/regular/postseason), quarter scores, venues, attendance, broadcast networks, per-game player stats (10 categories), team-level aggregates, injuries, drives, scoring plays, weather, officials, betting odds (opening/current/closing), and discovered API links.

**Agent features:** operate-tier MCP tools, coverage/quality monitoring, multi-season backfill orchestration, incremental SQLite→PostgreSQL push, correction proposal queue, and an in-repo Skill at `skills/nfl-db/SKILL.md`. See `AGENT_PLATFORM_PLAN.md` for the full roadmap.

### Components

| Component | Project | Description |
|-----------|---------|-------------|
| **REST API** | `WebScraper.Api` | Read-only endpoints + admin write endpoints (JWT/API key auth) |
| **Admin Dashboard** | `WebScraper.Api` | Blazor Server UI at `/admin/*` (MudBlazor dark theme) |
| **MCP Server** | `WebScraper.Mcp` | 36 Claude-callable tools over the API (stdio transport) |
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

The API must be running for the MCP server to work. Create an API key via the admin dashboard at `/admin/api-keys` (use `operate` scope for scrape/backfill tools, `admin` for push/backup/corrections). See `skills/nfl-db/SKILL.md` and `src/WebScraper.Mcp/README.md` for the full tool catalog.

## Admin Dashboard

The dashboard at `/admin/*` provides a visual interface for managing the entire system:

| Page | Path | Access | Description |
|------|------|--------|-------------|
| Login | `/admin/login` | Public | Email/password login form |
| Dashboard | `/admin` | All roles | Entity counts, recent jobs, system health |
| Jobs | `/admin/jobs` | All roles | Job table (auto-refreshes every 5s) with status filter |
| New Scrape | `/admin/scrapes/new` | Admin, Operator | Trigger scrapes — teams, games, stats, backfill, odds-poll |
| Backfill | `/admin/backfill` | Admin, Operator | Multi-season backfill control — start, pause/resume, progress, optional backup |
| Coverage | `/admin/coverage` | All roles | Week-by-week coverage table + regular-season heat map |
| Data Quality | `/admin/quality` | All roles | Open findings with scan and repair enqueue |
| Corrections | `/admin/corrections` | Admin | Approve/reject agent-proposed data corrections |
| API Keys | `/admin/api-keys` | Admin | Create/revoke API keys (plaintext shown once on create) |
| Users | `/admin/users` | Admin | Create users, assign roles (Admin/Operator/Viewer) |
| Deleted Items | `/admin/deleted-items` | Admin | Review and restore soft-deleted records |
| Push to Server | `/admin/push` | Admin | Batched, resumable SQLite → PostgreSQL push |
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

### Making API Calls

**Base URL (local dev):** `http://localhost:5080`

The API key is **never** passed as a query parameter (`?apikey=...` will not work). Use an HTTP header instead.

| Call type | Auth header | Used for |
|-----------|-------------|----------|
| GET read endpoints | `X-Api-Key: <plaintext-key>` | Teams, players, games, venues, status |
| GET/POST admin endpoints | `Authorization: Bearer <jwt-token>` | Login, scrapes, jobs, user/key management |

Create an API key at `/admin/api-keys` (plaintext shown once) or use Swagger at `/swagger` → **Authorize** → enter your key.

#### GET requests (API key)

Every read call is a plain `GET`. Put filters and pagination in the query string; put the key in the header.

```bash
# List teams (page 1, 25 per page)
curl -s -H "X-Api-Key: YOUR_KEY_HERE" \
  "http://localhost:5080/api/v1/teams"

# Filter + pagination
curl -s -H "X-Api-Key: YOUR_KEY_HERE" \
  "http://localhost:5080/api/v1/teams?conference=AFC&page=1&pageSize=50"

# Single resource by path parameter
curl -s -H "X-Api-Key: YOUR_KEY_HERE" \
  "http://localhost:5080/api/v1/teams/by-abbreviation/KC"

# Games for a season/week
curl -s -H "X-Api-Key: YOUR_KEY_HERE" \
  "http://localhost:5080/api/v1/games?season=2025&week=1"

# Quick data-health check (no auth on /health, but status needs a key)
curl -s -H "X-Api-Key: YOUR_KEY_HERE" \
  "http://localhost:5080/api/v1/status"
```

**List response shape** — paged endpoints return JSON like:

```json
{
  "items": [ { "id": 1, "name": "Kansas City Chiefs", "abbreviation": "KC", "...": "..." } ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 32,
  "totalPages": 2,
  "hasPrevious": false,
  "hasNext": true
}
```

Useful response headers on list calls: `X-Total-Count` (total rows), `X-Correlation-Id` (request trace id).

#### POST requests (JWT)

Admin/write endpoints require a JWT. Log in once, then pass the token on every subsequent call.

**Step 1 — Login (no auth required):**

```bash
curl -s -X POST "http://localhost:5080/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"your-password"}'
```

**Response:**

```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-07-02T20:00:00Z",
  "userId": "...",
  "email": "admin@example.com",
  "roles": ["Admin"]
}
```

**Step 2 — Use the token** (replace `TOKEN` with the value from `token`):

```bash
# Who am I? (any role)
curl -s -H "Authorization: Bearer TOKEN" \
  "http://localhost:5080/api/v1/auth/me"

# Trigger a scrape — no body needed for teams/players
curl -s -X POST "http://localhost:5080/api/v1/scrape/teams" \
  -H "Authorization: Bearer TOKEN"

# Trigger games scrape — JSON body required
curl -s -X POST "http://localhost:5080/api/v1/scrape/games" \
  -H "Authorization: Bearer TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"season":2025,"week":1}'

# Trigger stats scrape — season AND week required
curl -s -X POST "http://localhost:5080/api/v1/scrape/stats" \
  -H "Authorization: Bearer TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"season":2025,"week":1}'

# Poll job status (Operator+)
curl -s -H "Authorization: Bearer TOKEN" \
  "http://localhost:5080/api/v1/jobs/42"

# Create a new user (Admin only)
curl -s -X POST "http://localhost:5080/api/v1/auth/users" \
  -H "Authorization: Bearer TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"email":"operator@example.com","password":"ChangeMe123!","role":"Operator"}'

# Create an API key (Admin only) — save plaintextKey immediately
curl -s -X POST "http://localhost:5080/api/v1/api-keys" \
  -H "Authorization: Bearer TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"CI pipeline","scopes":["read"]}'
```

Scrape POSTs return **202 Accepted** with a `ScrapeJobDto` body and a `Location` header pointing at `/api/v1/jobs/{id}`.

**POST body reference:**

| Endpoint | Body | Notes |
|----------|------|-------|
| `POST /api/v1/auth/login` | `{"email","password"}` | Returns JWT |
| `POST /api/v1/auth/users` | `{"email","password","role"}` | Role: `Admin`, `Operator`, or `Viewer` |
| `POST /api/v1/scrape/teams` | _(none)_ | Operator+ |
| `POST /api/v1/scrape/players` | _(none)_ | Operator+ |
| `POST /api/v1/scrape/games` | `{"season":2025,"week":1}` | `season` required; `week` optional |
| `POST /api/v1/scrape/stats` | `{"season":2025,"week":1}` | Both required |
| `POST /api/v1/scrape/all` | `{"season":2025,"week":1}` | `season` required; `week` optional |
| `POST /api/v1/scrape/backfill` | `{"season":2006,"endSeason":2025}` | Multi-season backfill (Operator+) |
| `POST /api/v1/scrape/odds-poll` | `{"season":2026}` | Poll ESPN pickcenter for odds snapshots |
| `POST /api/v1/api-keys` | `{"name","scopes":["read"],"expiresAt":null}` | Admin only |
| `POST /api/v1/push?resume=true&reset=false` | _(none)_ | Admin — batched SQLite → PostgreSQL |
| `POST /api/v1/backup` | _(none)_ | Admin — timestamped SQLite backup |

#### Common errors

| Status | Meaning |
|--------|---------|
| `401 Unauthorized` | Missing/invalid API key or JWT |
| `403 Forbidden` | Valid JWT but wrong role for the endpoint |
| `404 Not Found` | Resource id doesn't exist |
| `429 Too Many Requests` | Rate limit (60/min); check `Retry-After` header |

Errors use [RFC 7807 Problem Details](https://datatracker.ietf.org/doc/html/rfc7807) JSON (`title`, `status`, `detail`).

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
| GET | `/api/v1/games/{id}/drives` | Drive chart for a game |
| GET | `/api/v1/games/{id}/scoring-plays` | Scoring plays for a game |
| GET | `/api/v1/games/{id}/weather` | Game-day weather |
| GET | `/api/v1/games/{id}/officials` | Referee crew |
| GET | `/api/v1/games/{id}/odds` | Betting odds snapshots |
| GET | `/api/v1/venues` | Paged venue list, optional `?state=`, `?isIndoor=` |
| GET | `/api/v1/venues/{id}` | Single venue |
| GET | `/api/v1/status` | Entity counts + freshest update timestamp |

### Operate Endpoints (API Key `operate`/`admin` or JWT Operator/Admin)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/v1/scrape/{type}` | Trigger scrape — types: `teams`, `players`, `games`, `stats`, `all`, `backfill`, `odds-poll` |
| GET | `/api/v1/jobs` | List scrape jobs (paged, optional `?status=`) |
| GET | `/api/v1/jobs/{id}` | Single job status |
| GET | `/api/v1/jobs/{id}/children` | Child jobs for a backfill parent |
| POST | `/api/v1/jobs/{id}/retry` | Re-queue a completed/failed job |
| GET | `/api/v1/coverage` | Expected-vs-actual coverage per week |
| POST | `/api/v1/coverage/refresh` | Recompute coverage (+ optional repair enqueue) |
| GET | `/api/v1/quality/findings` | Open data-quality findings |
| POST | `/api/v1/quality/scan` | Run quality rules scan |
| POST | `/api/v1/quality/repairs` | Enqueue repair jobs from findings |
| GET | `/api/v1/gaps` | Ranked coverage + quality gaps |
| GET | `/api/v1/backfill/estimate` | Workload estimate for a season range |
| POST | `/api/v1/backfill` | Start multi-season backfill (`backupFirst` in body) |
| GET | `/api/v1/backfill/{id}/progress` | Aggregate backfill progress + ETA |
| POST | `/api/v1/backfill/{id}/pause` | Pause a running backfill |
| POST | `/api/v1/backfill/{id}/resume` | Resume a paused backfill |
| GET | `/api/v1/schema` | Entity schema introspection |
| GET | `/api/v1/schema/dictionary` | Human-readable data dictionary |
| POST | `/api/v1/query/stats` | Parameterized stats aggregation (whitelist, no raw SQL) |
| GET | `/api/v1/skill` | Agent Skill markdown (`skills/nfl-db/SKILL.md`) |
| GET | `/api/v1/events` | Replay scrape events (SignalR catch-up) |

### Admin Endpoints (JWT Admin)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/v1/auth/login` | Exchange email/password for JWT |
| GET | `/api/v1/auth/me` | Current user profile + roles |
| POST | `/api/v1/auth/users` | Create user |
| GET | `/api/v1/auth/users` | List all users |
| GET/POST/DELETE | `/api/v1/api-keys` | API key management |
| GET | `/api/v1/deleted-items` | List soft-deleted items |
| POST | `/api/v1/deleted-items/{type}/{id}/restore` | Restore soft-deleted item |
| GET | `/api/v1/push/status` | Latest push session checkpoint |
| POST | `/api/v1/push?resume=&reset=` | Batched, resumable SQLite → PostgreSQL push |
| POST | `/api/v1/backup` | Create timestamped SQLite backup |
| GET | `/api/v1/backup` | List existing backups |
| GET/POST | `/api/v1/corrections` | List/propose data corrections |
| POST | `/api/v1/corrections/{id}/approve` | Approve a correction (Admin) |
| POST | `/api/v1/corrections/{id}/reject` | Reject a correction (Admin) |

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

# Push local SQLite to remote PostgreSQL (batched, resumable)
dotnet run --project src/WebScraper.Cli -- push
dotnet run --project src/WebScraper.Cli -- push --resume
dotnet run --project src/WebScraper.Cli -- push --reset

# Backup local SQLite database
dotnet run --project src/WebScraper.Cli -- backup
```

### Recommended Scrape Order

For a **single season**, if running commands individually:

1. `teams` — populates the teams table
2. `players` — current-season roster enrichment (optional for historical; players are discovered from box scores)
3. `games --season <year>` — needs team-seasons for home/away references
4. `stats --season <year> --week <n>` — needs games; also creates historical players from box scores

The `all` command handles steps 1–3 automatically.

For a **multi-season historical load** (2006–2025), use the backfill job instead:

```bash
# Via API (Operator JWT) or dashboard at /admin/backfill
POST /api/v1/backfill  {"startSeason":2006,"endSeason":2025,"backupFirst":true}

# Or MCP
nfl_trigger_scrape(type=backfill, season=2006, endSeason=2025)
```

Back up SQLite first (`backup` CLI command or `nfl_backup_database`). Monitor progress at `/admin/backfill` or via `nfl_get_backfill_progress`. Publish to PostgreSQL with `push` when coverage is green.

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

Then use the CLI (`push`, `push --resume`, `push --reset`) or the dashboard at `/admin/push`, or the API:

```bash
# Fresh push
curl -X POST -H "Authorization: Bearer TOKEN" "http://localhost:5080/api/v1/push"

# Resume interrupted push
curl -X POST -H "Authorization: Bearer TOKEN" "http://localhost:5080/api/v1/push?resume=true"

# Check push status
curl -H "Authorization: Bearer TOKEN" "http://localhost:5080/api/v1/push/status"
```

Push runs in batched stages (default 500 rows per batch, configurable via `Push:BatchSize`). Progress is checkpointed in the local SQLite `DatabasePushSessions` table so interrupted pushes can resume.

### SQLite Backup

Back up before a long backfill session (Phase E requirement):

```bash
dotnet run --project src/WebScraper.Cli -- backup
# or POST /api/v1/backup (Admin JWT)
```

Copies `data/nfl_data.db` to `data/backups/nfl_data_{timestamp}.db` and prunes to the last 3 copies (`Backup:RetainCount` in config).

## Database

EF Core with code-first migrations. The database is created and migrated automatically at startup for both the API and CLI.

### Schema (domain + ops tables)

| Table | Description |
|-------|-------------|
| Franchises | Stable franchise identity (handles relocations) |
| TeamSeasons | Team name/city/abbr per season (FK target for games) |
| Teams | Current-team convenience rows |
| Players | Rosters keyed on `EspnId` when available |
| PlayerTeamSeasons | Player ↔ team-season roster membership |
| Games | Schedules with quarter scores, venues, broadcast, season type |
| PlayerGameStats | Per-game stats across 10 categories (~40 columns) |
| Venues | Stadiums (name, city, state, grass/indoor) |
| TeamGameStats | Team-level per-game aggregates |
| Injuries | Player injury reports per game |
| GameDrives | Drive chart data |
| ScoringPlays | Scoring play summaries |
| GameWeathers | Game-day weather |
| GameOfficials | Referee crews |
| GameOdds | Betting odds snapshots (opening/current/closing per sportsbook) |
| ApiLinks | Discovered ESPN API endpoints |
| SeasonCoverages | Expected-vs-actual coverage snapshots per week |
| DataQualityFindings | Quality rule findings with repair payloads |
| DataCorrections | Agent-proposed corrections (approval queue) |
| ApiKeys | DB-backed API keys (SHA-256 hashed) |
| ApiQueryLogs | Observability log of every API request |
| ScrapeJobs | Persistent scrape job queue with status tracking |
| ScrapeEvents | Transactional outbox for real-time scrape notifications |
| DatabasePushSessions | Incremental push checkpoints |

All domain entities support soft delete and data lineage tracking (`IAuditableEntity` + `ISoftDeletable`).

## Testing

```bash
dotnet test                                    # Run all 284 tests
dotnet test --verbosity normal                 # Verbose output
dotnet test tests/WebScraper.Core.Tests        # Core tests only
```

## Agent Integration (MCP + Skill)

The MCP server exposes **36 tools** prefixed `nfl_*` across four tiers:

| Tier | Examples | API key scope |
|------|----------|---------------|
| Read | `nfl_list_teams`, `nfl_get_game`, `nfl_get_game_odds` | `read` |
| Operate | `nfl_trigger_scrape`, `nfl_get_backfill_progress`, `nfl_find_gaps` | `operate` |
| Publish | `nfl_trigger_push`, `nfl_backup_database` | `admin` |
| Propose | `nfl_propose_correction` | `admin` |

Full catalog: `src/WebScraper.Mcp/README.md`. Agent runbooks: `skills/nfl-db/SKILL.md` (also served at `GET /api/v1/skill`).

## Project Structure

```
WebScraper.sln
skills/nfl-db/SKILL.md          # Agent Skill (versioned in-repo)
src/
├── WebScraper.Core/            # Shared library: models, DbContext, repos, scrapers, coverage, push
├── WebScraper.Cli/             # Console app: CLI + interactive REPL
├── WebScraper.Api/             # Web API + Blazor admin dashboard
│   ├── Auth/                   # Identity, JWT, API key, cookie auth
│   ├── Controllers/            # REST endpoints (20+ controllers)
│   ├── Components/             # Blazor Server pages (MudBlazor)
│   │   ├── Layout/             # AdminLayout (dark theme), EmptyLayout (login)
│   │   └── Pages/Admin/        # 12 dashboard pages + 2 dialog components
│   ├── Hubs/                   # SignalR hub for real-time scrape events
│   ├── Middleware/             # Query logging, rate limiting
│   └── Services/               # Job queue, event relay, odds scheduler, API key mgmt
└── WebScraper.Mcp/             # MCP server: 36 Claude-callable tools
tests/
└── WebScraper.Core.Tests/      # 284 xUnit tests
```

## License

This project is for educational purposes.
