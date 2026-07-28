# AGENTS.md

Project overview, architecture, and standard build/test/run commands live in `README.md` and `CLAUDE.md`. Read those first. This file only captures durable, non-obvious guidance for agents working in the cloud environment.

## Cursor Cloud specific instructions

### Environment
- .NET 8 SDK is preinstalled (system dependency, baked into the VM snapshot). The startup update script only runs `dotnet restore`. If `dotnet` is somehow missing, install with `sudo apt-get install -y dotnet-sdk-8.0`.
- Standard commands (build/test/run/CLI) are documented in `README.md` — do not duplicate them here.

### Services (all .NET 8, `net8.0`)
- `src/WebScraper.Api` — primary host: REST API + Blazor Server admin dashboard + SignalR, all in one process. Also runs the in-process scrape job worker and event relay.
- `src/WebScraper.Cli` — standalone scraper/REPL; shares the same SQLite DB as the API.
- `src/WebScraper.Mcp` — stdio MCP server; only useful when launched by an MCP client (build-verify only, do not expect it to "run" standalone).
- `src/WebScraper.Core` — library (not runnable). `tests/WebScraper.Core.Tests` — 224 xUnit tests, run with `dotnet test`.

### Non-obvious caveats
- Default DB is embedded SQLite at `data/nfl_data.db` (repo root). No external DB server is needed. Both the API and CLI auto-apply EF migrations on startup and create the file if missing. Because the path is relative, run both from the repo root so they share the same DB.
- The API and CLI default to the `Espn` data provider, which requires outbound internet to `site.api.espn.com` (no API key). ESPN scrapes work in this environment. On startup the CLI performs a live ESPN connectivity check before any command (including `status`/`list`), so those still need internet.
- Admin dashboard login and JWT endpoints require secrets in the git-ignored `src/WebScraper.Api/appsettings.Local.json` (`Jwt:SigningKey`, `InitialAdmin:Email`/`Password`, and optionally a bootstrap `ApiKeys` entry). This file is NOT committed and NOT recreated by the update script — a fresh VM has no admin login until you create it. `README.md` §2 documents the shape; generate a key with `openssl rand -base64 48`. The initial admin is only seeded when the Identity user table is empty.
- Run the API in `Development` (`ASPNETCORE_ENVIRONMENT=Development`) to expose Swagger at `/swagger`. Default URL is `http://localhost:5080` (set `ASPNETCORE_URLS=http://localhost:5080`).
- Read endpoints (`/api/v1/*`) need an `X-Api-Key` header; write endpoints need an `Authorization: Bearer <jwt>` from `POST /api/v1/auth/login`. Scrape POSTs are async: they return 202 + a job id; poll `GET /api/v1/jobs/{id}` for `Succeeded`/`Failed` (a full players scrape takes ~45s).
- `dotnet restore` emits a `NU1902` moderate-severity advisory for `AngleSharp 1.4.0`. It is a warning only and does not fail the build.
