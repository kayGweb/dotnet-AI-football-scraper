# api-sports migration: coordinator script for Grok, task cards for Cursor agents

Paste Part A into Grok as its standing instructions. Grok hands out the task cards in Part B one at a
time to Cursor agents, in dependency order, and relays reports back to the owner. Each task card is
written so a Cursor agent can start cold: repo, base branch, files, steps, acceptance checks.

Source of truth for the design: `API_SPORTS_MIGRATION_PLAN.md` (this repo) and
`docs/plans/api-sports-migration-plan.md` (chatbot repo). Same document, two copies.

---

## Part A. Grok coordinator instructions

```text
You are the delivery coordinator for the api-sports.io migration across two repositories:

  CHATBOT  = github.com/kayGweb/nextjs-ai-football-chatbot   (Next.js 15, pnpm, Drizzle, Vercel)
  SCRAPER  = github.com/kayGweb/dotnet-AI-football-scraper    (.NET 8, EF Core, xUnit)

BASE_BRANCH = claude/happy-albattani-46aq85
  (the integration branch in both repos: it holds the plan and the api-sports fixtures, every card
   lands on it, and main is untouched until the whole migration is done and the owner merges it)

Your job
1. Dispatch task cards from Part B to Cursor agents, one card per agent, in dependency order.
   A card may only start when every card in its "Depends on" line is reported DONE.
   Cards with no shared dependency may run in parallel on different agents.
2. Give the agent the card text verbatim, plus the "Rules for every agent" block below.
3. Collect the agent's report (format at the end of Part A). Verify it names a pushed branch, a PR
   link, and the exact test/lint commands that passed. If anything is missing, send the agent back.
4. Tell the owner, in one short message per finished card: card id, PR link, anything the agent
   flagged as BLOCKED or QUESTION. Never merge PRs yourself. The owner merges.
5. Keep a running board: card id | status (QUEUED / RUNNING / BLOCKED / DONE) | agent | PR.

Gates you must respect
- S1c (stats services) and S1d (injury service) need fixtures the owner has not captured yet
  (game-player-stats.json, game-team-stats.json, injuries-team.json, teams.json). Do not dispatch
  them until the owner says those files are committed. Ask the owner for them when S1b is DONE.
- C3c (injury report via bridge) needs S2 merged (InjuryReports table) and S1d producing rows.
- S5 (Droplet) is ops plus code. Dispatch the code part; the owner runs the Droplet commands.
- Never ask an agent to run anything against the production Neon database. Local SQLite for the
  scraper, a local or branch Postgres for the chatbot. Migrations are generated, not applied to prod.

Rules for every agent (include this block in every dispatch)
- Branch from BASE_BRANCH as feature/api-sports/<card-id>-<short-name>. Open a PR back to
  BASE_BRANCH. Do not merge. Do not force-push.
- Keep the PR to the card. If you find something outside it, note it in your report; do not fix it.
- Secrets: never commit an API key, connection string or password. appsettings.Local.json and
  .env.local are git-ignored and stay that way. Checked-in config keeps ApiKey empty.
- Read the plan section named in the card before writing code. Read the fixture README
  (tests/WebScraper.Core.Tests/Fixtures/ApiSports/README.md or lib/nfl-data/fixtures/README.md).
- Chatbot: run `pnpm lint` and `pnpm test:unit` and paste the tail of the output in the PR.
  Add new test files to the test:unit script in package.json.
- Scraper: run `dotnet build` and `dotnet test` from the repo root and paste the summary line.
- Update CLAUDE.md in the repo you touched when you add a file, provider, endpoint, job type,
  config section or command. Keep the edit to the rows you changed.
- If a card's assumption is wrong (a field is not where the card says, a test cannot pass as
  written), stop, write what you found, and report QUESTION. Do not improvise around it.

Report format (the agent sends this to you; you forward the essentials to the owner)
  CARD: <id>
  STATUS: DONE | BLOCKED | QUESTION
  BRANCH: feature/api-sports/...
  PR: <url>
  CHECKS: <commands run and their final lines>
  FILES: <added/changed, one per line>
  NOTES: <deviations from the card, things found outside scope, open questions>
```

---

## Part B. Task cards

Dependency graph (left must be DONE before right starts):

```
SCRAPER:  S2 ─┐
              ├─► S1b ─► S1c (needs stats fixtures)
  S1a ────────┘    └───► S1d (needs injuries fixture) ─► S3 ─► S4
  S5 (independent)

CHATBOT:  C1 ─► C2
          C1 ─► C4
          C3a ─► C3b
          C3a + S2 + S1d ─► C3c
          everything ─► C5
```

Parallel lanes to start today: S1a, S2, S5, C1, C3a.

**Done on scraper integration branch `claude/happy-albattani-46aq85` (do not re-dispatch):** S1a (#46), S2 (#47), S1b (#49), S5 (#48). **Next scraper cards when fixtures exist:** S1c, then S1d, then S3 and S4.

---

### S1a. ApiSports DTOs and mappings (scraper)

Repo: SCRAPER. Depends on: nothing. Plan section: S1.

Goal: the pure parsing layer for api-sports, with tests against the committed fixtures. No HTTP, no DI.

Files to add:
- `src/WebScraper.Core/Services/Scrapers/ApiSports/ApiSportsDtos.cs`
- `src/WebScraper.Core/Services/Scrapers/ApiSports/ApiSportsMappings.cs`
- `tests/WebScraper.Core.Tests/Scrapers/ApiSports/ApiSportsDtoTests.cs`
- `tests/WebScraper.Core.Tests/Scrapers/ApiSports/ApiSportsMappingsTests.cs`

Steps:
1. Read `tests/WebScraper.Core.Tests/Fixtures/ApiSports/README.md`, `games-by-date.json`, `players-by-team.json`.
2. DTOs with `System.Text.Json` attributes: `ApiSportsEnvelope<T>` (`get`, `parameters` as `Dictionary<string,string>`, `errors` as `JsonElement` because the provider returns `[]` or an object, `results`, `response` as `List<T>`), `ApiSportsGameItem` (`game`, `league`, `teams`, `scores` exactly as the fixture nests them), `ApiSportsPlayer`. Add `bool HasErrors` on the envelope that is true when `errors` is a non-empty object or array.
3. Mappings:
   - `TeamIdToAbbreviation`: the 26 ids in the plan §1 table. Leave a clearly marked `// TODO(S1b): ids 7, 8, 9, 19, 22, 27 from teams.json` for the six missing teams. Return `null` for unknown ids.
   - `ParseWeek(string stage, string week)` → `(NflSeasonType SeasonType, int Week)?`. `"Regular Season"` + `"Week N"` → Regular, N. `"Pre Season"` + `"Week N"` → Preseason, N. `"Post Season"` + `Wild Card`=1, `Divisional Round`=2, `Conference Championships`=3, `Super Bowl`=4. Anything else (NCAA division names, bare numbers) → `null`.
   - `ParseStatus(string shortCode)` → `(string GameStatus, bool IsFinal, bool IsScheduled)`. `FT`/`AOT` final; `NS` scheduled; `Q1..Q4`, `HT`, `OT` in progress; `CANC`, `PST` not final, not scheduled.
   - `ParseHeight("5' 8\"")` → `"5-8"` (the format `EspnPlayerService` already writes); `ParseWeight("203 lbs")` → `203`; both return `null` on garbage.
   - `IsNfl(ApiSportsGameItem)` → `league.id == 1`.
4. Tests: deserialize both fixtures; assert 16 games, 13 are NFL, Cal Poly has `overtime` 7 both sides, the `NS` game has all-null scores, every NFL team id resolves, each `ParseWeek` case above, each `ParseStatus` case, height/weight parsing including null.

Acceptance: `dotnet test` green; no changes outside the four files plus CLAUDE.md project-structure rows.

---

### S2. Identity migration, InjuryReport table, upsert match order (scraper)

Repo: SCRAPER. Depends on: nothing. Plan section: S2.

Goal: schema and repository changes that let api-sports rows coexist with ESPN rows.

Files:
- `src/WebScraper.Core/Models/InjuryReport.cs` (new)
- `src/WebScraper.Core/Data/AppDbContext.cs`
- `src/WebScraper.Core/Data/Repositories/GameRepository.cs`, `PlayerRepository.cs`, `IPlayerRepository.cs`
- `src/WebScraper.Core/Data/Repositories/IInjuryReportRepository.cs`, `InjuryReportRepository.cs` (new)
- `src/WebScraper.Core/Extensions/ServiceCollectionExtensions.cs` (register the repo)
- `src/WebScraper.Core/Services/Quality/` (the "missing EspnId" rule)
- `src/WebScraper.Core/Services/Push/` (new `InjuryReports` stage after `Injuries`)
- `src/WebScraper.Core/Migrations/<timestamp>_ApiSportsProvider.cs` + Designer + snapshot (generated)
- tests under `tests/WebScraper.Core.Tests/Repositories/`

Steps:
1. `InjuryReport : IAuditableEntity, ISoftDeletable` with `Id`, `TeamSeasonId` (FK), `PlayerId?` (FK), `ExternalPlayerId` (string, required), `PlayerName`, `Position`, `Status`, `Description`, `ReportedAt?`, `SnapshotAt` (UTC). Unique index on `(ExternalPlayerId, SnapshotAt)`; index on `TeamSeasonId`. Global soft-delete filter like the other entities.
2. `AppDbContext`: `DbSet<InjuryReport>`; indexes on `Games (DataSource, DataSourceRecordId)` and `Players (DataSource, DataSourceRecordId)`.
3. `GameRepository.UpsertAsync`: after the `EspnEventId` lookup and before the natural-key lookup, add a lookup on `DataSource` + `DataSourceRecordId` when both are set. On update, also copy `DataSource`, `DataSourceFetchedAt`, `DataSourceRecordId` from the incoming game when set.
4. `PlayerRepository`: `GetByExternalIdAsync(string source, string recordId)` and `UpsertByExternalIdAsync(Player)`; mirror `UpsertByEspnIdAsync`.
5. `InjuryReportRepository`: `UpsertAsync` (key `ExternalPlayerId` + `SnapshotAt`), `GetCurrentAsync(string? teamAbbreviation)` returning the newest snapshot per `ExternalPlayerId`, optionally filtered by team through `TeamSeason → Franchise`.
6. Quality rule: the rule that flags players with no `EspnId` must pass when `DataSourceRecordId` is set. Rename it in code and in the finding text to "missing external id".
7. Push stage `InjuryReports` after `Injuries` in the stage enum and runner.
8. Generate the migration: `dotnet ef migrations add ApiSportsProvider --project src/WebScraper.Core --startup-project src/WebScraper.Cli`. Commit the three generated files. Do not hand-edit them.
9. Tests: upsert by external id inserts then updates; game upsert matches an existing ESPN game by natural key and keeps `EspnEventId`; `GetCurrentAsync` returns the latest snapshot only.

Acceptance: `dotnet test` green; migration applies on a fresh SQLite (run the CLI `status` command once locally and paste the output); CLAUDE.md schema section lists `InjuryReports` and the new indexes.

---

### S1b. ApiSports team and game services, provider wiring (scraper)

Repo: SCRAPER. Depends on: S1a, S2. Plan section: S1.

Files:
- `src/WebScraper.Core/Services/Scrapers/ApiSports/ApiSportsTeamService.cs`, `ApiSportsGameService.cs`
- `src/WebScraper.Core/Models/DataProvider.cs` (add `ApiSports`)
- `src/WebScraper.Core/Services/DataProviderFactory.cs` (`case "apisports"`)
- `src/WebScraper.Core/Services/ConsoleDisplayService.cs` (provider list + display name "API-Sports")
- `src/WebScraper.Cli/appsettings.json`, `src/WebScraper.Api/appsettings.json` (`Providers.ApiSports` block; set `DataProvider` to `ApiSports` in the Api file only)
- `src/WebScraper.Core/Services/Scrapers/ApiSports/ApiSportsMappings.cs` (fill the six missing team ids from `teams.json` if the owner has committed it; otherwise leave the TODO)
- tests: `ApiSportsTeamServiceTests.cs`, `ApiSportsGameServiceTests.cs`, add a case to `DataProviderFactoryTests.cs`

Steps:
1. Both services extend `BaseApiService` like `SportsDataGameService`. Constructor injects the same repositories plus `ITeamSeasonRepository` and `IVenueRepository`.
2. Add a protected helper in the ApiSports folder (not in `BaseApiService`): `FetchEnvelopeAsync<T>(url)` that calls `FetchJsonAsync<ApiSportsEnvelope<T>>`, and if `HasErrors` logs the `errors` JSON and returns `null`. Callers return `ScrapeResult.Failed("api-sports error: ...")` on `null`.
3. `ApiSportsTeamService.ScrapeTeamsAsync`: `GET teams?league=1&season={current season}` (use `NflSeasonSchedule.GetCurrentSeason(DateTime.UtcNow)`), map by `TeamIdToAbbreviation`, skip and warn on unknown ids, upsert `Team` with `DataSource="ApiSports"`, `DataSourceRecordId=id`, `DataSourceFetchedAt=UtcNow`.
4. `ApiSportsGameService`: both `ScrapeGamesAsync` overloads call `GET games?league=1&season={season}` once, filter `IsNfl`, filter by `ParseWeek` season type (and week for the week overload). For each game: resolve both teams via mapping → `ITeamRepository.GetByAbbreviationAsync` → `ITeamSeasonRepository.EnsureFromTeamAsync(team, season)`; `GameDate = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime`; scores and quarters from `scores`, all null when `IsScheduled`; `GameStatus` from `ParseStatus`; `HomeWinner` only when final; venue upsert by name+city when name is not null (venues have no api-sports id; reuse `IVenueRepository` the way `EspnGameService` does, but key on name); `DataSource="ApiSports"`, `DataSourceRecordId=game.id`. Return `ScrapeResult.Succeeded(count, ...)`.
5. Wiring per `DataProviderFactory` pattern: `AddApiHttpClient` for team, player (placeholder registration can point at `EspnPlayerService` is NOT acceptable; register a `NotSupportedPlayerScraperService` stub that returns `ScrapeResult.Failed("ApiSports player scrape lands in S1c")` so the factory contract holds), game, and stats (same stub approach for stats). `appsettings` block: `BaseUrl https://v1.american-football.api-sports.io`, `AuthType Header`, `AuthHeaderName x-apisports-key`, `ApiKey ""`, `RequestDelayMs 1200`.
6. Tests with the mocked `HttpMessageHandler` pattern used in `EspnGameServiceTests`: feed `games-by-date.json`; assert NCAA games are skipped, 13 games upserted, the `NS`-style case stores null scores (craft one NFL `NS` item in the test from the fixture), the London game resolves WAS as home, a game whose team id is unknown is skipped with a warning and does not fail the batch; an envelope with `errors: {"token": "Error/Missing application key"}` returns `Failed`.

Acceptance: `dotnet test` green; `dotnet run --project src/WebScraper.Cli -- teams --source ApiSports` with an empty key fails cleanly with the api-sports error message (paste it); CLAUDE.md provider table gains the ApiSports row.

---

### S1c. ApiSports player and stats services (scraper)

Repo: SCRAPER. Depends on: S1b, and fixtures `players-by-team.json` (committed), `game-player-stats.json`, `game-team-stats.json` (owner to capture). Plan section: S1.

Files: `ApiSportsPlayerService.cs`, `ApiSportsStatsService.cs`, DTO additions in `ApiSportsDtos.cs`, tests, factory registration replacing the S1b stubs.

Steps:
1. Read the two new fixtures first; add DTOs that match them exactly. If the shape differs from what the plan assumed (categories named `passing`, `rushing`, `receiving`, `defensive`, `interceptions`, `fumbles`, `kicking`, `punting`, `kick_returns`, `punt_returns` with name/value pairs), follow the fixture and note the difference in your report.
2. `ApiSportsPlayerService.ScrapePlayersAsync(teamId)`: `GET players?team={apiSportsTeamId}&season={season}`; map `height`/`weight` with the S1a parsers, `Position`, `JerseyNumber` (null when 0), `College`, `Status = group`, `IsActive = group is Offense/Defense/Special Teams`; `UpsertByExternalIdAsync` with `DataSource="ApiSports"`; ensure a `PlayerTeamSeason` row. `ScrapeAllPlayersAsync` loops the 32 mapped teams.
3. `ApiSportsStatsService.ScrapePlayerStatsAsync(season, week, seasonType)`: load the week's final games from `IGameRepository` where `DataSource="ApiSports"`; for each, `GET games/statistics/players?id={DataSourceRecordId}` and `GET games/statistics/teams?id=...`; map to `PlayerGameStats` (discover unknown players by api-sports id, same as `EspnStatsService` does by athlete id) and `TeamGameStats`. Upsert through the existing repositories.
4. Tests from fixtures: player count, height/weight parsing, group→status; stats: one known player's passing line maps to the right columns, team stats upsert for both teams.

Acceptance: `dotnet test` green; the S1b stubs are gone; CLAUDE.md ESPN-style table for ApiSports lists all four services.

---

### S1d. Injury scraping and job type (scraper)

Repo: SCRAPER. Depends on: S2, S1b, fixture `injuries-team.json` (owner to capture). Plan sections: S1, S3 (job type only).

Files:
- `src/WebScraper.Core/Services/Scrapers/IScraperService.cs` (add `IInjuryScraperService { Task<ScrapeResult> ScrapeCurrentInjuriesAsync(); }`)
- `src/WebScraper.Core/Services/Scrapers/NoOpInjuryScraperService.cs`
- `src/WebScraper.Core/Services/Scrapers/ApiSports/ApiSportsInjuryService.cs`
- `src/WebScraper.Core/Models/ScrapeJob.cs` (`ScrapeJobType.Injuries`)
- `src/WebScraper.Api/Controllers/ScrapeController.cs` (`POST /api/v1/scrape/injuries`)
- `src/WebScraper.Api/Services/ScrapeJobWorker.cs` (branch)
- `src/WebScraper.Api/Components/Pages/Admin/NewScrape.razor` (option)
- `DataProviderFactory` (register for ApiSports; NoOp otherwise, same as `IOddsPollService`)
- tests

Steps: for each of the 32 mapped teams `GET injuries?team={id}`; one `SnapshotAt` per run; map to `InjuryReport` (`ExternalPlayerId` = player id from the response, `TeamSeasonId` via `EnsureFromTeamAsync`, `PlayerId` when a `Players` row with that external id exists); upsert. Worker branch mirrors the Teams branch. Controller returns 202 like the others and writes the `JobQueued` event.

Acceptance: `dotnet test` green; Swagger shows the new route; CLAUDE.md job types and endpoints updated.

---

### S3. Schedulers and the Neon poll guard (scraper)

Repo: SCRAPER. Depends on: S1d. Plan section: S3.

Files: `src/WebScraper.Api/Services/ScheduleRefreshScheduler.cs`, new `InjuryRefreshScheduler.cs`, `ScrapeEventRelay.cs`, `ApiServiceCollectionExtensions.cs`, `src/WebScraper.Api/appsettings.json`.

Steps:
1. `ScheduleRefreshScheduler`: new setting `ScheduleRefresh:StatsAfterWeekEnds` (default true). When true and a Games job for the season succeeded within the interval, enqueue one `Stats` job for the most recent week whose games are all final and which has no succeeded Stats job yet. Use `DependsOnJobId` on the games job.
2. `InjuryRefreshScheduler : BackgroundService` with section `InjuryRefresh { Enabled, Days: ["Wednesday","Friday"], HourUtc: 15 }`; enqueues one `Injuries` job per listed day, skipping if one already succeeded that day. `Source` from `ScraperSettings.DataProvider`.
3. `ScrapeEventRelay`: replace the constant 1-second poll with `Events:RelayPollSeconds` (default 10). Skip the DB query entirely when no `ScrapeJob` is `Running` or `Queued` and no hub client is connected; track connections with `OnConnectedAsync`/`OnDisconnectedAsync` counters on `ScraperHub`. Document in a comment that this exists to let Neon's compute suspend.
4. Tests for the pure parts (day/hour matching, the "skip when idle" predicate).

Acceptance: `dotnet test` green; `appsettings.json` has the two new sections; CLAUDE.md lists both schedulers and the relay setting.

---

### S4. Injuries endpoint, status fields, MCP tool (scraper)

Repo: SCRAPER. Depends on: S1d. Plan section: S4.

Files: `src/WebScraper.Api/Controllers/InjuriesController.cs` (new), `Dtos/InjuryReportDto.cs`, `Mapping/EntityMappings.cs`, `Controllers/StatusController.cs` + `Dtos/StatusDto.cs` (`Provider`, `LastInjurySnapshotAt`, `LastStatsJobAt`), `src/WebScraper.Mcp/NflApiClient.cs`, `Tools/GameTools.cs` or new `InjuryTools.cs` (`nfl_get_current_injuries`), `src/WebScraper.Mcp/README.md`.

Steps: `GET /api/v1/injuries/current?team=` under `RequireReadScope`, grouped by team abbreviation, `Meta` envelope populated. Status fields from the repositories. MCP tool wraps it. README tool count goes to 47.

Acceptance: `dotnet build` green for Api and Mcp; a curl against the local API with the bootstrap key returns the grouped shape (paste it); CLAUDE.md endpoint and tool tables updated.

---

### S5. Droplet deployment artifacts (scraper)

Repo: SCRAPER. Depends on: nothing. Plan section: S5.

Files: `Dockerfile` (repo root, multi-stage: `mcr.microsoft.com/dotnet/sdk:8.0` build and publish of `src/WebScraper.Api`, `mcr.microsoft.com/dotnet/aspnet:8.0` runtime, non-root user, `EXPOSE 8080`, `ASPNETCORE_URLS=http://+:8080`), `.dockerignore`, `deploy/docker-compose.yml` (services `api` and `caddy`; `api` reads `env_file: /opt/webscraper/.env`; `caddy` reverse-proxies `${SCRAPER_HOST}` to `api:8080`), `deploy/Caddyfile`, `deploy/.env.example` (every variable from plan §5, values blank), `deploy/README.md` (Ubuntu 24.04 steps: Docker install, `ufw allow 22,80,443`, create `/opt/webscraper/.env` with `chmod 600`, `docker compose up -d`, first-login and password rotation, `docker compose logs -f api`, update procedure).

Rules: `Program.cs` must not need changes; if it does, stop and report QUESTION. The compose file must not include a Postgres service. Health check in compose uses `/health/live`.

Acceptance: `docker build -t webscraper-api .` succeeds locally and `docker run --rm -e DatabaseProvider=Sqlite webscraper-api` starts and answers `/health/live` (paste the curl). CLAUDE.md gets a "Deployment" subsection pointing at `deploy/README.md`.

---

### C1. Provider interface, api-sports provider, SportsRadar moved to fallback (chatbot)

Repo: CHATBOT. Depends on: nothing. Plan section: C1.

Files to add: `lib/nfl-data/types.ts`, `lib/nfl-data/index.ts`, `lib/nfl-data/providers/api-sports.ts`, `lib/nfl-data/providers/sportsradar.ts`, `lib/nfl-data/providers/api-sports.test.ts`. Files to change: `lib/sportsradar.ts` (becomes `export * from "./nfl-data/providers/sportsradar"`), `.env.example`, `package.json` (`test:unit`).

Steps:
1. Read `lib/nfl-data/fixtures/README.md` and both fixtures. Read `lib/sportsradar.ts`, `lib/db/seed/sync-schedule-helpers.ts`, `lib/db/sync-scores-helpers.ts` to see what consumers need.
2. `types.ts`: `NormalizedGameStatus = "scheduled" | "in_progress" | "final" | "postponed" | "cancelled"`; `NormalizedGame { sourceGameId: string; providerGameId: string; seasonYear: number; seasonType: "preseason" | "regular" | "postseason"; week: number; kickoff: Date; homeAbbrev: string; awayAbbrev: string; homeScore: number | null; awayScore: number | null; quarters: { home: (number | null)[]; away: (number | null)[]; homeOT: number | null; awayOT: number | null } | null; status: NormalizedGameStatus; venueName: string | null; venueCity: string | null; broadcast: string | null }`; `NflDataProvider { name: "api-sports" | "sportsradar"; isConfigured(): boolean; getSeasonSchedule(year, seasonType): Promise<NormalizedGame[]>; getWeekSchedule(year, seasonType, week): Promise<NormalizedGame[]>; getGamesForDate(date: Date): Promise<NormalizedGame[]> }`.
3. `providers/api-sports.ts`: base `https://v1.american-football.api-sports.io`, header `x-apisports-key` from `API_SPORTS_KEY`; `fetch` with `next: { revalidate: 60 }`; envelope check that throws a readable error when `errors` is non-empty; `TEAM_ID_TO_ABBREV` with the 26 known ids (same table as the plan §1; export it so a test can assert against `NflTeam` seeds); week/stage parsing and status mapping identical to the scraper's S1a rules; filter `league.id === 1`; `sourceGameId = "apisports:" + game.id`. Export pure helpers (`parseWeek`, `parseStatus`, `toNormalizedGame`) separately from the fetching functions so tests need no network.
4. `providers/sportsradar.ts`: move the current `lib/sportsradar.ts` content verbatim, keep every existing export, add `toNormalizedGame(srGame, week, seasonType, year)` and an `NflDataProvider` object `sportsRadarProvider` built on the existing functions. `sourceGameId = "sportradar:" + id` to match rows already in the database.
5. `index.ts`: `getScheduleProvider()` reads `NFL_DATA_SOURCE` (`api-sports` when `API_SPORTS_KEY` is set and the var is unset, else `sportsradar`); `getLiveProvider()` reads `LIVE_DATA_SOURCE` and falls back to `getScheduleProvider()`. Unknown values throw at call time with a clear message.
6. Tests (node test runner, see `lib/db/seed/sync-schedule.test.ts` for style): load `lib/nfl-data/fixtures/games-by-date.json`, run `toNormalizedGame` over the response, assert 13 NFL games, NCAA dropped, London game is WAS home / IND away with `final` status and 13-30, timestamps map to the expected ISO instants, every `parseWeek` and `parseStatus` case, and that every id in `TEAM_ID_TO_ABBREV` is one of the 32 abbreviations in `lib/db/seed/seed-teams.ts`.
7. `.env.example`: add `API_SPORTS_KEY=`, `NFL_DATA_SOURCE=api-sports`, `LIVE_DATA_SOURCE=` with one-line comments; move the `SPORTSRADAR_*` lines under a `# Fallback live provider` comment.

Acceptance: `pnpm lint` clean, `pnpm test:unit` green including the new file; nothing else imports from `lib/nfl-data/providers/sportsradar` yet (the re-export keeps existing imports working); CLAUDE.md project structure lists `lib/nfl-data/`.

---

### C2. Source-agnostic schedule and score sync, re-key safety (chatbot)

Repo: CHATBOT. Depends on: C1. Plan section: C2.

Files: `lib/db/seed/sync-schedule-helpers.ts`, `lib/db/sync-schedule.ts`, `lib/db/seed/sync-schedule.ts` (CLI), `lib/db/sync-scores-helpers.ts`, `lib/db/sync-scores.ts`, `app/(chat)/api/cron/sync-schedule/route.ts`, `app/(chat)/api/cron/sync-scores/route.ts`, new `scripts/rekey-games.ts`, `package.json` (`db:rekey-games`), `vercel.json`, tests `lib/db/seed/sync-schedule.test.ts`, `lib/db/sync-scores-helpers.test.ts`.

Steps:
1. `buildGameRow` takes a `NormalizedGame` (not `SRGame`). Delete `SR_ALIAS_TO_ABBREV` from the helpers; abbreviation mapping now lives in each provider. Keep `buildTeamLookup`, `resolveTeamId` (now takes an abbreviation), `parseKickoffUtc` is no longer needed because `kickoff` is already a `Date`.
2. `upsertScheduleGame`: when no row matches `sourceGameId`, look up `(seasonId, week, homeTeamId, awayTeamId)`. If found, `update` that row's `sourceGameId` and schedule fields and return `"rekeyed"`. Extend the return type and the `SyncSummary` with `rekeyed`.
3. `syncSchedule(provider = getScheduleProvider())`: for `api-sports` call `getSeasonSchedule` once and process all regular-season games; for `sportsradar` keep the existing window logic and per-week calls. Export `syncScheduleFromSportsRadar` as a deprecated alias that calls `syncSchedule(sportsRadarProvider)` so nothing breaks mid-PR, then remove it once callers are updated in this same PR.
4. Scores: `isSyncableFinalGame(game: NormalizedGame)` is `status === "final"` with both scores present. `syncScores(provider)`: when `getWeeksAwaitingScores` is non-empty, call `getGamesForDate` for today and yesterday (UTC) and update matching rows by `sourceGameId`; write quarters when present (`homeQ1..homeOT`, `awayQ1..awayOT`). Return early with a logged "no games awaiting scores" when the week list is empty.
5. Cron routes call the new functions; response shapes keep their existing keys and add `rekeyed` and `provider`.
6. `scripts/rekey-games.ts`: `--from=sportradar --to=apisports [--season=YYYY] [--apply]`. Dry run prints a table of (week, home, away, old id → new id, matched/unmatched). Only `--apply` writes. It uses the target provider's `getSeasonSchedule` once and the natural key to match.
7. `vercel.json`: `sync-scores` to `*/15 * * * *`.
8. Tests: rekey case (existing `sportradar:` row, incoming `apisports:` row with same natural key → one row, new id); unchanged row → `skipped`; new row → `upserted`; `isSyncableFinalGame` for each status; quarters written when present.

Acceptance: `pnpm lint`, `pnpm test:unit` green; `grep -r "SRGame" lib app` returns only `lib/nfl-data/providers/sportsradar.ts`; CLAUDE.md commands list `db:rekey-games`; runbook section 2 step 2 says `pnpm db:sync-schedule` now does one api-sports call.

---

### C3a. Bridge schema, Drizzle table filter, sourcePlayerId migration (chatbot)

Repo: CHATBOT. Depends on: nothing (reads the scraper's existing table shapes; `InjuryReports` is added in C3c). Plan section: C3.

Files: `lib/db/bridge/scraper-schema.ts` (new), `drizzle.config.ts`, `lib/db/schema.ts` (`nflPlayer.sourcePlayerId`), `lib/db/migrations/0026_*.sql` + meta (generated with `pnpm db:generate`), `docs/database.md`.

Steps:
1. Open the scraper repo's `src/WebScraper.Core/Models/` (`Franchise.cs`, `TeamSeason.cs`, `Player.cs`, `Game.cs`, `PlayerGameStats.cs`, `TeamGameStats.cs`) and `Data/AppDbContext.cs` for table and column names (EF default: table = DbSet name, columns = property names, PascalCase, quoted). Define read-only Drizzle `pgTable`s in `scraper-schema.ts` with only the columns the bridge needs: Franchises (`Id`, `Abbreviation`), TeamSeasons (`Id`, `FranchiseId`, `Season`), Players (`Id`, `Name`, `Position`, `JerseyNumber`, `Height`, `Weight`, `College`, `Status`, `IsActive`, `DataSource`, `DataSourceRecordId`, `IsDeleted`), Games (`Id`, `Season`, `SeasonType`, `Week`, `GameDate`, `HomeTeamSeasonId`, `AwayTeamSeasonId`, `GameStatus`, `DataSource`, `DataSourceRecordId`, `IsDeleted`), PlayerGameStats (every stat column that has a counterpart in `nflPlayerStats`), TeamGameStats (the columns that feed `nflTeamStats`). Add a file header: "Read-only mirror of the dotnet scraper's tables in the shared database. Never add to drizzle.config schema. Never write."
2. `drizzle.config.ts`: add `tablesFilter` listing every table in `lib/db/schema.ts` by name (generate the list from the schema export, do not hand-type it twice). Add a comment explaining why (`db:push` would otherwise offer to drop the scraper's tables).
3. `nflPlayer.sourcePlayerId: varchar(100)` unique nullable. `pnpm db:generate` → migration 0026. Commit SQL and meta.
4. `docs/database.md`: new "Shared database with the scraper" section: table ownership, the filter, the rule that production only receives migrations.

Acceptance: `pnpm lint` green; `pnpm db:check` passes; migration 0026 present; the Drizzle filter contains exactly the chatbot's table names (paste the list in the PR).

---

### C3b. Tuesday player-stats bridge (chatbot)

Repo: CHATBOT. Depends on: C3a. Plan section: C3.

Files: `lib/db/bridge/sync-player-stats.ts`, `lib/db/bridge/map-player-stats.ts` (pure mapping), `lib/db/bridge/map-player-stats.test.ts`, `app/(chat)/api/cron/sync-stats/route.ts`, `scripts/bridge-stats.ts`, `package.json` (`db:bridge-stats`, test:unit), `vercel.json` (Tuesday 10:00 UTC), `lib/db/queries.ts` (helpers if needed), docs.

Steps:
1. Select scraper `Games` where `DataSource = 'ApiSports'`, `Season = :year`, `Week = :week`, `IsDeleted = false`, `GameStatus` final. Join `NflGame` on `sourceGameId = 'apisports:' || Games."DataSourceRecordId"`. Skip games without a match and count them.
2. For each matched game, read `PlayerGameStats` joined to scraper `Players` (`DataSource='ApiSports'`). Upsert `NflPlayer` on `sourcePlayerId = 'apisports:' || Players."DataSourceRecordId"` (name, position, jersey, height, weight, college, isActive, team via the game's TeamSeason → Franchise abbreviation → `NflTeam`). Upsert `NflPlayerStats` on `(playerId, gameId)`; add a unique index on those two columns in a migration 0027 if none exists.
3. Aggregate `TeamGameStats` for the season into `NflTeamStats` per team (yards, turnovers, sacks, penalties, time of possession averaged as `mm:ss`). Leave wins/losses/points to the standings rebuild.
4. Cron route with the same `CRON_SECRET` guard as the other crons. Default target week = the most recent week with all games completed. Query param `?week=` to override.
5. CLI `pnpm db:bridge-stats --season=2026 --week=1 [--through=5]`.
6. Tests on the pure mapper: a scraper stats row with known values maps to the right `nflPlayerStats` columns; nulls pass through; sacks keep one decimal.

Acceptance: `pnpm lint`, `pnpm test:unit` green; the route returns `{ gamesMatched, gamesUnmatched, playersUpserted, statRowsUpserted, week, seasonYear }`; runbook section 5 row "Player stats" changes to "works via Tuesday bridge"; CLAUDE.md commands and crons updated.

---

### C3c. Injury report from the bridge (chatbot)

Repo: CHATBOT. Depends on: C3a, and the scraper's S2 merged and S1d producing rows. Plan section: C3.

Files: `lib/db/bridge/scraper-schema.ts` (add `InjuryReports`, `TeamSeasons` link), `lib/db/bridge/injuries.ts` (`getCurrentInjuries({ team?, playerName? })`), `lib/ai/tools/get-injury-report.ts`, tests.

Steps: latest `SnapshotAt` per `ExternalPlayerId` (window function or `DISTINCT ON`), join TeamSeason → Franchise for the abbreviation, optional filters. The tool returns `{ source: "scraper-db", asOf, team?, injuredPlayers: [{ name, position, status, description, reportedAt }] }` or the SportsRadar shape when `LIVE_DATA_SOURCE=sportsradar`. Tool description drops the word SportsRadar.

Acceptance: `pnpm lint`, `pnpm test:unit` green; a local run against a database seeded with two snapshots returns only the newer one (paste the output).

---

### C4. Live tools and prompt wording (chatbot)

Repo: CHATBOT. Depends on: C1. Plan section: C4.

Files: `lib/ai/tools/get-live-scores.ts`, `get-upcoming-schedule.ts`, `get-roster-moves.ts`, `lib/ai/prompts.ts`, `app/(chat)/api/chat/route.ts` (comment on the `liveTools` group only), `docs/ai-sdk.md`, `docs/prompts.md`.

Steps:
1. `getLiveScores`: `getLiveProvider().getGamesForDate(today)` filtered to the requested week when given; output keeps today's shape (`games[]` with `homeTeam`, `awayTeam`, `points`, `status`, `venue`) plus `source: provider.name` and `asOf: new Date().toISOString()`. When the provider is not configured return the same `error` string shape as today with the provider's env var named.
2. `getUpcomingSchedule`: read `NflGame` for the week via `lib/db/queries.ts` (add a small query if none fits); only call the provider when the week has no rows. Same output shape as today.
3. `getRosterMoves`: unchanged when `SPORTSRADAR_API_KEY` is set. Otherwise return `{ source: "none", unavailable: true, suggestion: "Use searchNFLNews for transactions and roster moves." }`.
4. `prompts.ts`: replace "SportsRadar" with "the live data provider" in the conductor prompt; add a line: "For trades, signings, cuts and roster moves, use searchNFLNews unless getRosterMoves reports it is available." Keep every other rule.
5. Tests: a unit test on the pure formatting function you extract from `getLiveScores` (NormalizedGame[] → tool output).

Acceptance: `pnpm lint`, `pnpm test:unit` green; `grep -rn "SportsRadar" lib/ai` only hits `lib/nfl-data/providers/sportsradar.ts` re-exports and the roster-moves tool; docs updated.

---

### C5. Documentation and configuration sweep (chatbot)

Repo: CHATBOT. Depends on: C1, C2, C3b, C4. Plan section: C5.

Files: `CLAUDE.md`, `docs/go-live-runbook.md`, `docs/ai-sdk.md`, `docs/api-routes.md`, `docs/prompts.md`, `docs/database.md`, `.env.example`.

Steps: make every description of the data path match the code after C1–C4: env table (section 1), one-time load (section 2), budget (section 4, now "api-sports Pro; usage well under cap"), what the bot can answer (section 5), new crons, new commands, `lib/nfl-data/` and `lib/db/bridge/` in the project tree, SportsRadar described as fallback only. Bump the CLAUDE.md document version and date.

Acceptance: `grep -rn "SportsRadar" docs CLAUDE.md` shows only fallback mentions; `pnpm lint` green (markdown is not linted, but run it anyway so the PR has a check line).

---

## Part C. Owner checklist (not for agents)

- `claude/happy-albattani-46aq85` is the integration branch in both repos. Leave `main` alone until every card is DONE, then open one PR per repo from that branch into `main`.
- Capture the remaining fixtures and commit them to both fixture folders: `teams.json`, `game-player-stats.json`, `game-team-stats.json`, `injuries-team.json`, `games-season.json` (see plan §6 for the exact calls). S1c and S1d wait on these.
- Keys: `API_SPORTS_KEY` in Vercel; `ScraperSettings__Providers__ApiSports__ApiKey` and the Neon connection string in `/opt/webscraper/.env` on the Droplet. Never in a PR.
- Before C2's re-key `--apply`: run the week-number check from plan §1 (api-sports says 2026-10-04 is Week 4).
- Review and merge PRs in dependency order. Deploy the chatbot with `NFL_DATA_SOURCE=sportsradar` first, verify, then flip to `api-sports`.
