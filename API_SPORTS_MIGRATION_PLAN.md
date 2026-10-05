# Plan: Move the NFL data feed to api-sports.io

**Status:** v2, 2026-10-05 (supersedes v1 of 2026-10-04)
**Repos:** `dotnet-AI-football-scraper` (ApiSports provider, Droplet deployment), `nextjs-ai-football-chatbot` (direct api-sports provider for schedule/scores, shared-database bridge for stats and injuries, SportsRadar kept as a fallback)

## What changed from v1

Three answers changed the shape of the plan:

1. **api-sports Pro tier** (about $15/month). The daily quota is no longer the constraint, so the rule "only the scraper may hold the key" is gone. Both apps call api-sports, each for what it is good at.
2. **The scraper runs on a DigitalOcean Droplet and writes to the chatbot's Neon Postgres.** One database, two schemas-by-prefix (`Teams`, `Games`, `PlayerGameStats` next to `NflTeam`, `NflGame`, `NflPlayerStats`). The chatbot reads scraper tables directly. No REST client, no `NFL_API_URL` / `NFL_API_KEY` in the chatbot.
3. **Transactions stay on Grok news.** No api-sports source exists; a `NflTransaction` table is a later option if a feed appears.

---

## 0. Decisions

| Decision | Choice | Why |
|----------|--------|-----|
| Upstream | api-sports.io American Football v1, `https://v1.american-football.api-sports.io`, header `x-apisports-key`, NFL is `league=1`, Pro tier | Replaces the SportsRadar trial |
| Schedule and scores (pick'em critical path) | **Chatbot calls api-sports directly** through a new provider in `lib/nfl-data/providers/api-sports.ts` | About 150 lines, runs on Vercel cron, does not depend on the Droplet being up. One call returns the whole season |
| Players, box scores, team stats, injuries | **Scraper** with a new `ApiSports` provider, on a schedule, writing to Neon | This is what the scraper is for; it has repositories, lineage, coverage and quality rules already |
| How the chatbot gets stats and injuries | **Bridge job reading the scraper's tables in the same Neon database** (Drizzle, read-only) | Replaces the dead one-shot migration 0011. Both apps key games on the same api-sports game id, so the join is exact |
| Scraper REST API and MCP | Kept, for the admin dashboard, agents, and Claude skills | Not on the chatbot's path any more |
| SportsRadar | Moved to `lib/nfl-data/providers/sportsradar.ts`, selectable with `NFL_DATA_SOURCE=sportsradar` / `LIVE_DATA_SOURCE=sportsradar` | Instant rollback with no deploy |
| Identity for api-sports rows in the scraper | Existing `DataSource` + `DataSourceRecordId` lineage columns plus new indexes, no per-provider id columns | One extra match step in `GameRepository.UpsertAsync` |
| History | 2006 to 2025 stays ESPN in the scraper; api-sports covers 2026 forward. Player crosswalk between the two is a later task | No reason to re-pull twenty seasons |

---

## 1. api-sports facts that shape the design

Verified from the vendor guide and search results; the docs site is not reachable from the build sandbox, so **field names are confirmed on Day 0 against real responses** (§6).

- Auth header `x-apisports-key`, GET only. Pro tier is thousands of requests a day and a per-minute cap (check the dashboard for the exact numbers; set `RequestDelayMs` from the per-minute figure).
- `GET /games?league=1&season=YYYY` returns the **whole season**, schedule and finals, in one call. `GET /games?league=1&date=YYYY-MM-DD` or `live=all` for game-day polling.
- `GET /games/statistics/players?id=` and `/games/statistics/teams?id=` are one call per game.
- `GET /players?team=&season=` and `GET /injuries?team=` are per team (32 calls per pass). Injuries have no league filter and no history.
- Shapes: `game.stage` is `Pre Season | Regular Season | Post Season`; `game.week` is a string (`Week 4`, `Wild Card`, `Divisional Round`, `Conference Championships`, `Super Bowl`); kickoff is `date.timestamp` (unix UTC); `status.short` is one of `NS, Q1, Q2, Q3, Q4, OT, HT, FT, AOT, CANC, PST`; scores are `scores.home.quarter_1..quarter_4, overtime, total`.
- Gaps: no transactions, no injury history, `coverage.injuries` on `/leagues` must be checked.

Expected daily usage with Pro: chatbot about 1 season call plus up to 50 score polls on a Sunday; scraper about 1 schedule call, 16 box scores on Tuesday, 32 injuries on Wed and Fri, 32 rosters on Tuesday. Well under any Pro cap.

---

## 2. Target architecture

```
                 ┌────────────────────────────────────────────────────────┐
api-sports.io ──►│ Next.js chatbot (Vercel)                               │
   (schedule,    │  lib/nfl-data/providers/api-sports.ts   ← primary      │
    scores)      │  lib/nfl-data/providers/sportsradar.ts  ← fallback     │
                 │  cron sync-schedule (daily) + sync-scores (15 min)     │
                 │     → NflGame  (pick week, locking, scoring unchanged) │
                 │  cron sync-stats (Tue) ← bridge reads scraper tables   │
                 │  getInjuryReport      ← bridge reads InjuryReports     │
                 │  getRosterMoves       ← Grok news (SportsRadar if key) │
                 └───────────────┬────────────────────────────────────────┘
                                 │ same Neon PostgreSQL
                 ┌───────────────┴────────────────────────────────────────┐
api-sports.io ──►│ dotnet scraper on a DigitalOcean Droplet (Docker)      │
   (players,     │  DataProvider=ApiSports → Teams, Players, Games,       │
    box scores,  │     PlayerGameStats, TeamGameStats, InjuryReports      │
    injuries)    │  REST /api/v1/* + /admin dashboard + MCP (ops only)    │
                 └────────────────────────────────────────────────────────┘
```

Both apps write the api-sports game id: the chatbot as `NflGame.sourceGameId = "apisports:<id>"`, the scraper as `Games.DataSourceRecordId = "<id>"` with `DataSource = "ApiSports"`. That shared key is what makes the bridge a plain join.

---

## 3. Scraper plan (`dotnet-AI-football-scraper`)

### S1. ApiSports provider

New folder `src/WebScraper.Core/Services/Scrapers/ApiSports/`, following the SportsData.io template:

| File | Purpose |
|------|---------|
| `ApiSportsDtos.cs` | Envelope `{get, parameters, errors, results, response}` plus team, game, player, player-statistics, team-statistics, injury DTOs, written against the Day 0 fixtures |
| `ApiSportsMappings.cs` | 32 team ids ↔ NFL abbreviations; `ParseWeek("Week 4" / "Wild Card" ...)` → `(NflSeasonType, int)` with postseason Wild Card=1 … Super Bowl=4 to match `NflSeasonSchedule`; `ParseStatus("FT")` → `GameStatus` + `IsFinal` |
| `ApiSportsTeamService` | `GET /teams?league=1&season=` → `Team` upsert by abbreviation |
| `ApiSportsGameService` | One `GET /games?league=1&season=` call, filtered client-side by stage and week. Scheduled games (`NS`) stored with **null** scores, kickoff from `date.timestamp` as UTC, quarter scores, `GameStatus`, `HomeWinner`, venue via `IVenueRepository` |
| `ApiSportsPlayerService` | `GET /players?team=&season=` × 32 → `Player` upsert by `(DataSource, DataSourceRecordId)` plus `PlayerTeamSeason` rows |
| `ApiSportsStatsService` | Per final game in the week: `/games/statistics/players?id=` → `PlayerGameStats` (discovers players by api-sports id like the ESPN path does by athlete id), `/games/statistics/teams?id=` → `TeamGameStats` |
| `ApiSportsInjuryService` | New `IInjuryScraperService.ScrapeCurrentInjuriesAsync()`: `/injuries?team=` × 32 → `InjuryReport` snapshot. `NoOpInjuryScraperService` for other providers (same pattern as `NoOpOddsPollService`) |

Wiring: `DataProvider.ApiSports`; `DataProviderFactory` `case "apisports"` with `AuthType=Header`, `AuthHeaderName=x-apisports-key`; `ConsoleDisplayService` provider list; `Providers.ApiSports` block in both `appsettings.json` files with `ApiKey: ""`; `DataProvider: "ApiSports"` in `src/WebScraper.Api/appsettings.json`.

api-sports returns HTTP 200 with a populated `errors` object on a bad key or exhausted quota. The ApiSports services check the envelope and return `ScrapeResult.Failed`, so a quota or key problem never looks like "0 games".

### S2. Schema (one migration, `ApiSportsProvider`)

- Index `(DataSource, DataSourceRecordId)` on `Games` and `Players`.
- `GameRepository.UpsertAsync` match order: `EspnEventId` → `(DataSource, DataSourceRecordId)` → natural key `(Season, SeasonType, Week, HomeTeamSeasonId, AwayTeamSeasonId)`. The natural key merges api-sports rows into games ESPN already loaded for 2026 instead of duplicating them. Updates also copy the three lineage columns.
- `PlayerRepository.UpsertByExternalIdAsync(source, recordId)`.
- New `InjuryReport` entity (`TeamSeasonId`, `PlayerId?`, `ExternalPlayerId`, `PlayerName`, `Position`, `Status`, `Description`, `ReportedAt`, `SnapshotAt` + auditable + soft-delete), unique on `(ExternalPlayerId, SnapshotAt::date)`, repository with `GetCurrentAsync(teamAbbr?)`.
- Quality rule "missing EspnId" → "missing external id" (`EspnId` or `DataSourceRecordId`).
- Push pipeline gets an `InjuryReports` stage (still useful for the local SQLite → Neon workflow).

### S3. Jobs and schedulers

- `ScrapeJobType.Injuries`; `POST /api/v1/scrape/injuries`; worker branch; New Scrape page option.
- `ScheduleRefreshScheduler` unchanged (12h, now one api-sports call). Add `StatsAfterWeekEnds: true` so a `Stats` job for the just-finished week is enqueued Tuesday morning through the existing games-then-stats dependency chain.
- New `InjuryRefreshScheduler` (config `InjuryRefresh`: `Enabled`, `Days` = Wed, Fri, `HourUtc`).
- **Neon cost guard:** `ScrapeEventRelay` polls the database every 1 second today, which keeps a Neon compute awake 24/7 and burns compute hours. Make the interval configurable (`Events:RelayPollSeconds`, default 10) and skip polling while no job is `Running` and no SignalR client is connected. `Jobs.razor` already polls only while the page is open. Rate-limit and query-log writers are fine (batched).

### S4. API surface

- `GET /api/v1/injuries/current?team=` (new `InjuriesController`, read scope) for the dashboard, MCP and any future consumer. Not on the chatbot's path.
- `/api/v1/status` adds `provider`, `lastInjurySnapshotAt`, `lastStatsJobAt` so the runbook can verify the schedulers.
- MCP `nfl_get_current_injuries` (47 tools). Skill runbook: "Switch provider to ApiSports and re-sync the current season".

### S5. Droplet deployment (minimum M5 slice)

- `Dockerfile` for `WebScraper.Api` (multi-stage, `dotnet publish -c Release`), `docker-compose.yml` (API + Caddy for TLS; no local Postgres in production).
- Droplet: Ubuntu 24.04, 1 vCPU / 2 GB (about $12/month; 1 GB is enough but tight for EF startup), Docker Engine, `ufw` allowing 22/80/443 only.
- Env file `/opt/webscraper/.env` (chmod 600), read by compose:

  ```env
  DatabaseProvider=PostgreSQL
  ConnectionStrings__DefaultConnection=Host=<neon-host>;Database=<db>;Username=<user>;Password=<pw>;SSL Mode=Require;Trust Server Certificate=true
  ScraperSettings__DataProvider=ApiSports
  ScraperSettings__Providers__ApiSports__ApiKey=PASTE_KEY_HERE
  Jwt__SigningKey=<openssl rand -base64 48>
  InitialAdmin__Email=you@example.com
  InitialAdmin__Password=<temporary, rotate after first login>
  ASPNETCORE_ENVIRONMENT=Production
  ```

  Use Neon's **direct** (non-pooler) connection string. EF Core migrations and Npgsql prepared statements do not get along with the PgBouncer pooler, and the scraper holds very few connections anyway.
- Caddy fronts `https://scraper.<your-domain>` → `/admin` dashboard, `/swagger` off in Production, `/api/v1/*` for MCP. Restrict `/admin` to your IP in Caddy if you want belt and braces.
- First boot: migrations run against Neon (separate `__EFMigrationsHistory` and `__AuthMigrationsHistory` tables, no clash with Drizzle's `drizzle.__drizzle_migrations`), `IdentitySeeder` creates the admin, you log in and rotate the password. Single instance only (worker and relay are single-instance by design).
- Ops: `docker compose pull && docker compose up -d` to deploy; a GitHub Actions workflow that builds the image to GHCR on push to `main` is a nice-to-have.

### S6. Tests

`tests/WebScraper.Core.Tests/Scrapers/ApiSports/`: mappings (32 teams, week/stage parsing, status mapping), game service (scheduled → null scores, final → totals and quarters, postponed → keeps id and moves week, envelope error → `Failed`), stats service, injury service. `DataProviderFactoryTests` gets an `apisports` case. Fixtures from Day 0 live in `tests/WebScraper.Core.Tests/Fixtures/ApiSports/`.

---

## 4. Chatbot plan (`nextjs-ai-football-chatbot`)

### C1. Provider interface, api-sports primary, SportsRadar fallback

New `lib/nfl-data/`:

| File | Purpose |
|------|---------|
| `types.ts` | `NormalizedGame` (`sourceGameId`, `homeAbbrev`, `awayAbbrev`, `kickoff`, `week`, `seasonType`, `status: scheduled \| in_progress \| final \| postponed \| cancelled`, scores, quarters, `venue`, `broadcast`), `NflDataProvider` (`getSeasonSchedule`, `getWeekSchedule`, `getGamesForDate`, `name`, `isConfigured`) |
| `providers/api-sports.ts` | `fetch` with `x-apisports-key: API_SPORTS_KEY`, same week/stage/status mappings as the scraper, team `code` → abbreviation map, envelope `errors` check |
| `providers/sportsradar.ts` | today's `lib/sportsradar.ts` moved verbatim plus a `toNormalizedGame` adapter; `lib/sportsradar.ts` becomes a re-export |
| `index.ts` | `getScheduleProvider()` / `getLiveProvider()` by `NFL_DATA_SOURCE` (`api-sports` default when `API_SPORTS_KEY` is set, else `sportsradar`) and `LIVE_DATA_SOURCE` |

### C2. Schedule and score sync become source-agnostic

- `buildGameRow` takes a `NormalizedGame`; source prefix per provider (`apisports:`, `sportradar:`).
- **Re-key gotcha:** production 2026 rows carry `sportradar:<id>` ids and the upsert conflicts on `sourceGameId`. `upsertScheduleGame` gets a fallback lookup by `(seasonId, week, homeTeamId, awayTeamId)` and rewrites `sourceGameId` on that row, so switching providers never duplicates the season. Unit test in `sync-schedule.test.ts`, plus `pnpm db:rekey-games --from=sportradar --to=apisports` (dry run by default) so the one-time switch is a visible step.
- `syncSchedule(provider)` uses the season endpoint (one call, always full); `syncScores(provider)` uses `getGamesForDate(today)` on game days, which covers every kicked-off week in one call instead of one per week. Quarter scores are written (columns exist, nothing fills them today).
- `vercel.json`: `sync-scores` to `*/15 * * * *`; the route returns early when no game kicked off in the last 6 hours so idle days cost nothing. `sync-schedule` stays daily.

### C3. Shared-database bridge for stats and injuries (the 2026 player-stats fix)

- `lib/db/bridge/scraper-schema.ts`: read-only Drizzle definitions for the scraper's `Franchises`, `TeamSeasons`, `Players`, `Games`, `PlayerGameStats`, `TeamGameStats`, `InjuryReports`. Only the columns the bridge reads. Never passed to drizzle-kit.
- `lib/db/bridge/sync-player-stats.ts`: for each `Games` row with `DataSource = 'ApiSports'` and a final status in the target week, join `NflGame` on `sourceGameId = 'apisports:' || "DataSourceRecordId"`, upsert `NflPlayer` on a new `sourcePlayerId` column (`'apisports:' || Players."DataSourceRecordId"`, migration `0026`, unique) and `NflPlayerStats` on `(playerId, gameId)`; aggregate `TeamGameStats` into `NflTeamStats` per season.
- Cron `app/(chat)/api/cron/sync-stats/route.ts` Tuesdays 10:00 UTC (`CRON_SECRET`), and `pnpm db:bridge-stats --season=2026 --week=N` for backfilling the weeks already played.
- `getInjuryReport` reads `InjuryReports` latest snapshot through the bridge schema (team or player filter in SQL). No api-sports call from the chatbot for injuries.
- **Drizzle footgun:** `drizzle.config.ts` has no `tablesFilter`, so `pnpm db:push` against a database that also holds the scraper's tables will offer to drop them. Add `tablesFilter` listing the chatbot's tables (or excluding the scraper's), and note in the runbook that `db:push` is never run against production; migrations are.

### C4. Live tools

| Tool | New source | Fallback |
|------|-----------|----------|
| `getLiveScores` | api-sports `getGamesForDate(today)` via the live provider, 60-second cache | SportsRadar when `LIVE_DATA_SOURCE=sportsradar` |
| `getUpcomingSchedule` | `NflGame` (already synced daily) with a provider call only when the week is missing | SportsRadar |
| `getInjuryReport` | bridge read of `InjuryReports` | SportsRadar |
| `getRosterMoves` | SportsRadar when its key exists; otherwise a structured "use searchNFLNews" result and the conductor prompt routes transactions to Grok news | Grok news |

Tool descriptions and `lib/ai/prompts.ts` drop hard-coded "SportsRadar" wording. Each tool returns `source` and `asOf`.

### C5. Docs and config

- `.env.example`: `API_SPORTS_KEY`, `NFL_DATA_SOURCE`, `LIVE_DATA_SOURCE`; `SPORTSRADAR_*` under a "fallback" comment.
- `CLAUDE.md`, `docs/go-live-runbook.md` (env table, budget section becomes api-sports Pro, section 5 table gains "player stats: works via Tuesday bridge"), `docs/ai-sdk.md`, `docs/api-routes.md`, `docs/prompts.md`, `docs/database.md` (bridge tables).
- Tests added to `test:unit`: `providers/api-sports.test.ts` (mapping + envelope), `sync-schedule.test.ts` re-key case, `sync-scores-helpers.test.ts` status mapping, `bridge/sync-player-stats.test.ts` (pure mapping of a `PlayerGameStats` row to `NflPlayerStats`).

---

## 5. Where you add the keys

| Key | Where | Name |
|-----|-------|------|
| api-sports key, chatbot | Vercel → Project → Settings → Environment Variables | `API_SPORTS_KEY` |
| Source switch, chatbot | Vercel env | `NFL_DATA_SOURCE=api-sports` (set `sportsradar` to roll back with no deploy); `LIVE_DATA_SOURCE` optional |
| api-sports key, scraper on the Droplet | `/opt/webscraper/.env` | `ScraperSettings__Providers__ApiSports__ApiKey` |
| Neon connection string, scraper | `/opt/webscraper/.env` | `ConnectionStrings__DefaultConnection` (direct, non-pooler string, `SSL Mode=Require`) |
| api-sports key, scraper local dev | `src/WebScraper.Cli/appsettings.Local.json` and `src/WebScraper.Api/appsettings.Local.json` (git-ignored) | `ScraperSettings:Providers:ApiSports:ApiKey` |
| SportsRadar, kept | leave `SPORTSRADAR_API_KEY` in Vercel | fallback provider and optional roster-moves source |

Local `appsettings.Local.json` shape for the scraper:

```json
{
  "ScraperSettings": {
    "DataProvider": "ApiSports",
    "Providers": {
      "ApiSports": {
        "BaseUrl": "https://v1.american-football.api-sports.io",
        "AuthType": "Header",
        "AuthHeaderName": "x-apisports-key",
        "ApiKey": "PASTE_KEY_HERE",
        "RequestDelayMs": 1200
      }
    }
  }
}
```

The checked-in `appsettings.json` keeps `ApiKey` empty. Keys go in the Local file, the Droplet env file, or Vercel; never in chat or source control.

---

## 6. Order of work

**Day 0, discovery.** With the key in the scraper's Local file, record real responses to `tests/WebScraper.Core.Tests/Fixtures/ApiSports/`: `teams.json`, `games-season.json`, `injuries-team.json`, `game-player-stats.json`, `game-team-stats.json`. Confirm the 32 team codes, week strings, status codes, Pro per-minute cap, and that `coverage.injuries` is true for the NFL.

**Week 1, chatbot C1 + C2.** Provider interface, api-sports provider, source-agnostic sync, re-key script, tests. Deploy with `NFL_DATA_SOURCE=sportsradar` first (identical behavior), then set `API_SPORTS_KEY` and `NFL_DATA_SOURCE=api-sports`, run the re-key dry run then `--apply`, trigger `sync-schedule` and `sync-scores` once by hand, verify with the runbook SQL (272 games, finals per week, 32 standings rows). **This alone unblocks the pick'em season.**

**Week 1, scraper S1 + S2 + S6.** Provider, migration, tests. Run a 2026 games scrape locally into SQLite and confirm 272 games (not 544) after merging with the ESPN rows.

**Week 2, scraper S3 + S4 + S5.** Schedulers, relay poll guard, injuries endpoint, Dockerfile, Droplet, Caddy, Neon env, first boot, admin login. Trigger a `Stats` job for the weeks already played and an `Injuries` job; confirm rows in Neon.

**Week 2, chatbot C3 + C4 + C5.** Bridge schema, `tablesFilter`, migration 0026, Tuesday stats cron, injuries from the bridge, live tools, prompts, docs. Run `pnpm db:bridge-stats` for weeks 1 through the current week. Ask the bot "compare Mahomes and Allen this season" and get 2026 numbers.

**Week 3, optional.** Postseason games from the same season call into the bracket, api-sports odds into the scraper's `GameOdds`, player crosswalk between ESPN and api-sports ids.

**Rollback at any point:** `NFL_DATA_SOURCE=sportsradar` in Vercel. The re-key script runs in both directions. The bridge is additive and can be paused by disabling its cron.

---

## 7. Risks

1. **Neon compute hours.** Two always-on consumers now touch the database; the scraper's 1-second relay poll is the one that would keep the compute from ever suspending. S3's guard fixes it. Watch the Neon usage page the first week.
2. **Shared database hygiene.** Never run `pnpm db:push` against production (C3 `tablesFilter` is the seatbelt). The scraper's EF migrations only touch its own tables and history tables.
3. **Mixed history.** Scraper `Players` will have both ESPN rows (2006 to 2025) and api-sports rows (2026 onward). Identity across the two is by name plus team-season until the crosswalk exists. The chatbot keys its own `NflPlayer` on `sourcePlayerId`, so chat answers are unaffected.
4. **Transactions.** Grok news only. Acceptable per your call; revisit if a feed appears.
5. **Team codes.** api-sports may use `WSH` / `JAX` style codes like SportsRadar. One mapping in `ApiSportsMappings.cs` and one in `providers/api-sports.ts`, both settled by the Day 0 capture and both covered by a 32-team test.
6. **Droplet is a single box.** Acceptable for a scheduler that runs a few jobs a week. Back up nothing on the Droplet; all state is in Neon.
