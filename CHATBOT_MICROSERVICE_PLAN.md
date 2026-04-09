# Chatbot Microservice Plan — NFL Data API

Transform the existing NFL web scraper console app into a containerized microservice that exposes a clean REST API (consumable by Claude from anywhere via an MCP server), broadcasts scrape events via SignalR, and provides a Blazor Server admin dashboard — all in a single deploy unit targeting DigitalOcean first, with a planned migration path to Azure App Service + MSSQL at scale.

## Locked decisions

| # | Decision |
|---|----------|
| 1 | Extract `WebScraper.Core` class library — **yes** |
| 2 | Job queue: **in-memory `Channel<T>` for MVP**, Hangfire deferred |
| 3 | MCP server: **included in initial milestones** |
| 4 | Deploy: **single unit** (API + SignalR + Blazor admin together) |
| 5 | DB: **PostgreSQL on DigitalOcean** now → migrate to **Azure App Service + MSSQL** at scale |
| 6 | Vercel AI SDK / normalizer sidecar: **dropped** — Claude Code handles schema + normalization manually |
| 7 | **Contract tests** for every provider (new first-class concern) |
| 8 | **Data lineage** columns on every entity |
| 9 | **Soft deletes** everywhere + admin review UI |
| 10 | **LLM query log** for observability into API consumers |

---

## Target solution structure

```
WebScraper.sln
├── src/
│   ├── WebScraper.Core/           # Models, DbContext, repos, scrapers (extracted)
│   ├── WebScraper.Api/            # ASP.NET Core Web API + SignalR + Blazor Server admin
│   ├── WebScraper.Mcp/            # MCP server (stdio) wrapping the API for Claude
│   └── WebScraper.Cli/            # Existing console app (renamed)
├── tests/
│   ├── WebScraper.Core.Tests/     # Existing tests (moved)
│   ├── WebScraper.Api.Tests/      # Controller + auth + integration tests
│   └── WebScraper.Contracts.Tests/# Provider contract tests (recorded fixtures)
├── deploy/
│   ├── Dockerfile
│   ├── docker-compose.yml         # api + postgres for local dev
│   └── do/                        # DigitalOcean App Platform spec
├── fixtures/                      # Recorded provider responses for contract tests
│   ├── espn/
│   ├── sportsdataio/
│   ├── mysportsfeeds/
│   ├── nflcom/
│   └── pfr/
└── .github/workflows/             # CI/CD
```

**Single-deploy note:** Blazor Server pages live inside `WebScraper.Api` at `/admin/*`. Same Kestrel host, same `IServiceProvider`, same DbContext pool. One container image, one deploy. Split later only if needed.

---

## Cross-cutting model changes (Phase 0)

### Data lineage
Every entity implements `IAuditableEntity`:

```csharp
public interface IAuditableEntity
{
    string? DataSource { get; set; }
    DateTime? DataSourceFetchedAt { get; set; }
    string? DataSourceRecordId { get; set; }
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
```

Stamped centrally via an EF Core `SaveChangesInterceptor` so `CreatedAt`/`UpdatedAt` are automatic. Scrapers stamp `DataSource`, `DataSourceFetchedAt`, `DataSourceRecordId` in their upserts.

### Soft delete
Every entity implements `ISoftDeletable`:

```csharp
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    string? DeletedBy { get; set; }
    string? DeleteReason { get; set; }
}
```

- Global query filter: `HasQueryFilter(e => !e.IsDeleted)`
- `IgnoreQueryFilters()` escape hatch for admin review
- `SaveChangesInterceptor` converts `Remove()` into soft delete + stamps metadata
- Admin `/admin/deleted-items` page lists soft-deleted rows with restore

### LLM query log
New `ApiQueryLog` entity captures every API consumer request (via middleware, async batch insert through a `Channel<ApiQueryLog>` background writer — never blocks requests). Blazor `/admin/api-usage` visualizes calls/day, top endpoints, latencies, error rates.

---

## Phase 0 — Core extraction (M0)

1. Create `src/WebScraper.Core/WebScraper.Core.csproj`
2. Move `Models/`, `Data/`, `Services/Scrapers/`, `Services/RateLimiterService.cs`, `Services/DataProviderFactory.cs`, `Services/DatabasePushService.cs`, `Migrations/` → `src/WebScraper.Core/`
3. Introduce `IAuditableEntity`, `ISoftDeletable`, `AuditingSaveChangesInterceptor` in Core
4. Add lineage + soft-delete fields to existing entities
5. Add `ApiQueryLog` entity
6. Generate migration `AuditableAndSoftDelete`
7. Update repositories' `UpsertAsync` to stamp lineage
8. Rename `WebScraper/` → `src/WebScraper.Cli/`, update `WebScraper.sln`, move tests → `tests/WebScraper.Core.Tests/`
9. Update `ServiceCollectionExtensions.AddWebScraperCore()` — same logic, now in Core
10. `dotnet build && dotnet test` — all green, CLI still works end-to-end

**Success criteria:** zero behavior change, existing `dotnet run --project src/WebScraper.Cli -- teams` produces identical output.

---

## Phase 1 — ASP.NET Core Web API (`WebScraper.Api`)

### Host
- `WebApplication.CreateBuilder`
- `services.AddWebScraperCore(configuration)` — reuses Phase 0
- Adds: controllers, Swagger (Swashbuckle + XML docs + example filters), SignalR, health checks, CORS, RateLimiter, ProblemDetails, ApiVersioning, ResponseCompression, ResponseCaching, Blazor Server

### Read endpoints (API-key, `scope=read`)
- `/api/v1/teams`, `/teams/{abbr}`, `/teams/{abbr}/players`, `/teams/{abbr}/games`
- `/api/v1/players`, `/players/{id}`, `/players/{id}/stats`
- `/api/v1/games`, `/games/{id}`, `/games/{id}/stats`, `/games/{id}/team-stats`, `/games/{id}/injuries`
- `/api/v1/venues`
- `/api/v1/stats/leaders?category=&season=`
- `/api/v1/search?q=`
- `/api/v1/status`

### Write endpoints (JWT, `role=Admin`)
- `POST /api/v1/scrape/{teams|players|games|stats|all}` → `202 Accepted` + `jobId`
- `GET /api/v1/jobs`, `GET /api/v1/jobs/{id}`
- `POST /api/v1/push` → SQLite→Postgres sync
- `GET/POST/DELETE /api/v1/api-keys`
- `GET /api/v1/deleted-items`, `POST /api/v1/deleted-items/{id}/restore`

### Response conventions
- Problem Details (RFC 7807) for errors
- ETag + If-None-Match
- Pagination: `?page=&pageSize=` + `X-Total-Count` + Link headers
- Data lineage in every response: `_meta: { source, fetchedAt, createdAt, updatedAt }`

### Auth
- **API key** middleware (`X-Api-Key`, SHA-256 hash compare, per-key rate limit)
- **JWT bearer** via ASP.NET Core Identity
- Policies: `RequireApiKey`, `RequireAdmin`

### Job queue (MVP, lightweight)
- `IJobQueue` backed by `System.Threading.Channels.Channel<ScrapeJob>`
- `ScrapeJobWorker : BackgroundService` consumes channel
- `ScrapeJob` table tracks state (Queued/Running/Succeeded/Failed), progress, errors
- On restart: re-queue any unfinished jobs (scrapers are idempotent via upsert)
- **Single-instance constraint documented** — scale-out needs Hangfire later

### OpenAPI quality (critical for Claude consumption)
- Rich XML docs on every action
- `[ProducesResponseType]` everywhere
- Example values via `Swashbuckle.AspNetCore.Filters`
- Tags per resource
- Published at `/openapi/v1.json` + Swagger UI at `/swagger`

### Middleware order
```
UseResponseCompression
UseRouting
UseCors
UseAuthentication          ← API key OR JWT
UseAuthorization
UseRateLimiter
UseApiQueryLogging         ← LLM query log
MapControllers
MapHub<ScraperHub>("/hubs/scraper")
MapBlazorHub
MapFallbackToPage("/_Host")
MapHealthChecks("/health/live", "/health/ready")
```

### Rate limiting
- .NET 8 `AddRateLimiter`, partition by API key
- 60 req/min default, `429` + `Retry-After`

### Health checks
- `/health/live` — process up
- `/health/ready` — DB reachable, migrations applied, providers reachable

---

## Phase 2 — SignalR broadcast

### Hub `/hubs/scraper`
Events: `JobQueued`, `JobStarted`, `JobProgress`, `JobCompleted`, `EntityUpserted`, `HealthChanged`
Groups: `jobs:{jobId}`, `entity:{type}`, `source:{provider}`

### Publisher
- `IScrapeEventPublisher` injected into scrapers
- Lightweight outbox: events written to `ScrapeEvent` table in same transaction as scrape upserts
- `ScrapeEventRelay : BackgroundService` polls unsent events and broadcasts via `IHubContext<ScraperHub>`
- Admin replays on reconnect via `GET /api/v1/events?since=`

### Auth
JWT (cookie from Blazor, bearer from external) or `scope=realtime` API key via `?access_token=`.

---

## Phase 3 — Blazor Server admin dashboard

**Lives inside `WebScraper.Api` at `/admin/*`. Cookie auth, ASP.NET Core Identity.**

### Pages
| Route | Purpose |
|-------|---------|
| `/admin` | Dashboard: health, DB counts, active jobs, last scrape per source, API usage graph |
| `/admin/health` | Expanded health UI, auto-refresh via SignalR |
| `/admin/jobs` | Live job list, kill/retry, streams from ScraperHub |
| `/admin/scrapes/new` | Trigger form (type × source × season × week) |
| `/admin/data/teams` | Browse/edit teams |
| `/admin/data/players` | Paged player grid |
| `/admin/data/games` | Games grid + box score drill-in |
| `/admin/data/quality` | Cross-provider diff viewer (Phase 7) |
| `/admin/deleted-items` | **All soft-deleted rows, restore button, reason shown** |
| `/admin/api-keys` | List/create/revoke, per-key usage |
| `/admin/api-usage` | **LLM query log: calls/day, top endpoints, latency percentiles** |
| `/admin/audit` | Append-only admin action log |
| `/admin/settings` | Provider config, rate limits, feature flags |

### UI stack
- **MudBlazor** (MIT, rich components)
- **ApexCharts.Blazor** for usage timelines + stat visualizations
- Live updates via `HubConnectionBuilder` → `ScraperHub`

### Roles
`Admin`, `Operator`, `Viewer`.

---

## Phase 4 — MCP server (`WebScraper.Mcp`)

### What it is
Small C# console app using the official **ModelContextProtocol** NuGet package. Runs as stdio MCP server for Claude Code / Claude Desktop.

### How it works
1. Reads OpenAPI spec at `$NFL_API_URL/openapi/v1.json` on startup
2. Auto-generates MCP tools from endpoints
3. Handles `tools/call` with authenticated HTTP requests (`X-Api-Key`)
4. Returns JSON tool results

### Tools
`nfl_list_teams`, `nfl_get_team`, `nfl_get_roster`, `nfl_get_games`, `nfl_get_game_detail`, `nfl_get_player`, `nfl_get_player_stats`, `nfl_search`, `nfl_get_status`, `nfl_trigger_scrape`, `nfl_get_job_status`.

### Config
```json
{
  "mcpServers": {
    "nfl": {
      "command": "dotnet",
      "args": ["run", "--project", "src/WebScraper.Mcp", "--no-build"],
      "env": {
        "NFL_API_URL": "https://nfl-api.example.com",
        "NFL_API_KEY": "sk_live_..."
      }
    }
  }
}
```

---

## Phase 5 — Contract tests (`WebScraper.Contracts.Tests`)

### Why first-class
Every provider can break upstream. Contract tests freeze known-good JSON shapes and replay them in CI.

### Layout
```
tests/WebScraper.Contracts.Tests/
├── Espn/
│   ├── TeamsContractTests.cs
│   ├── ScoreboardContractTests.cs
│   ├── SummaryContractTests.cs
│   └── RosterContractTests.cs
├── SportsDataIo/...
├── MySportsFeeds/...
├── NflCom/...
├── Pfr/...
└── Fixtures/            ← ../../fixtures/
```

### Workflow
1. **Record**: `dotnet run --project tools/FixtureRecorder -- --provider Espn --endpoint scoreboard --week 1` → saves `fixtures/espn/scoreboard-2025-wk1.json`
2. **Test**: contract test loads fixture via HttpClient test double, runs the scraper's parse logic, asserts entity counts + key fields
3. **Refresh** quarterly or on breakage

### CI integration
Runs on every PR with zero network calls.

---

## Phase 6 — Deploy (DigitalOcean first)

### Single container
One `Dockerfile` builds `WebScraper.Api` (API + SignalR + Blazor admin).

### Local dev
`deploy/docker-compose.yml`: api + postgres:16 + volumes → `docker compose up` → `http://localhost:5080/swagger`, `http://localhost:5080/admin`.

### DigitalOcean App Platform
`deploy/do/app.yaml`:
- Service: `api` (basic tier ~$12/mo)
- Database: managed Postgres dev tier (~$15/mo)
- Env vars: connection string, JWT signing key, admin bootstrap creds, provider API keys (DO secrets)
- Auto-deploy on push to main

**Estimated cost at MVP:** ~$30-40/mo.

### Future Azure path
When it's time to migrate:
- Swap `Npgsql.EntityFrameworkCore.PostgreSQL` → `Microsoft.EntityFrameworkCore.SqlServer` via config flag (already referenced)
- Deploy image to **Azure App Service for Containers** or **Container Apps**
- **Azure SQL** replaces managed Postgres
- EF migrations scripted against SQL Server (retest pipeline ahead of cutover)
- Introduce a `DatabaseDialect` abstraction early so SQL-specific features (`tsvector` vs `CONTAINS`) have pluggable implementations

### CI/CD
`.github/workflows/ci.yml`:
- build + test (unit + contract) on every PR
- build + push Docker image on merge to main
- trigger DO App Platform deploy

---

## Phase 7 — Production polish (post-MVP)

Ranked by value:
1. **Scheduled scrapes** via `IHostedService` + Cronos — nightly refresh, game-day stats pulls
2. **Cross-provider reconciliation** — `DataConflict` table surfaced in `/admin/data/quality`
3. **OpenTelemetry** (traces/metrics/logs) — export to Grafana Cloud
4. **Idempotency keys** on POST scrape endpoints
5. **Webhook subscriptions** — HMAC-signed
6. **Audit log** for every admin action
7. **Secrets**: DO secrets now, Azure Key Vault at cutover
8. **Backup automation** — nightly `pg_dump` → DO Spaces
9. **Graceful shutdown** — drain jobs on SIGTERM
10. **Correlation IDs** end-to-end
11. **Full-text search** — Postgres `tsvector` (SQL Server `CONTAINS` equivalent)
12. **Historical stat snapshots** — immutable `StatsSnapshot`
13. **Synthetic canary** — scheduled health scrape vs known-good counts

---

## Milestone sequence

| Milestone | Phases | Shippable outcome |
|-----------|--------|-------------------|
| **M0** | Phase 0 | Core extracted, lineage + soft delete + query log entities, tests green |
| **M1** | Phase 1 (read) + Phase 5 foundation | Read-only API live, contract tests for all providers, Swagger docs |
| **M2** | Phase 4 | MCP server — Claude can query NFL data from anywhere |
| **M3** | Phase 1 (write + jobs) + Phase 2 | Remote scraping with live SignalR progress |
| **M4** | Phase 3 | Blazor admin dashboard |
| **M5** | Phase 6 | DigitalOcean deployment, docker-compose, CI/CD |
| **M6** | Phase 7 (top items) | Scheduled scrapes, reconciliation, backups |

Each milestone is independently shippable and reviewable.
