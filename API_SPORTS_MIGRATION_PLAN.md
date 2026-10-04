# Plan: Move the NFL data feed to api-sports.io (scraper first, chatbot second)

**Status:** proposed, 2026-10-04
**Repos:** `dotnet-AI-football-scraper` (new provider + new endpoints), `nextjs-ai-football-chatbot` (read from the scraper, keep SportsRadar as an optional live fallback)

---

## 0. Decisions in one screen

| Decision | Choice | Why |
|----------|--------|-----|
| Primary upstream | **api-sports.io American Football v1** (`https://v1.american-football.api-sports.io`, header `x-apisports-key`, NFL = `league=1`) | Replaces the SportsRadar trial that is blocking launch |
| Who calls api-sports | **Only the scraper.** The chatbot never holds the api-sports key | One key, one quota (free plan is 100 requests/day, 10/minute); two apps polling the same key would halve it |
| How the chatbot gets data | Scraper REST API (`/api/v1/schedule`, `/api/v1/games`, new `/api/v1/injuries/current`) through a thin client, behind a `NFL_DATA_SOURCE` switch | The scraper already has a read API with API keys, pagination and lineage. The chatbot keeps writing `NflGame` itself so picks, locking and scoring do not change |
| SportsRadar | **Moved, not deleted.** Becomes `lib/nfl-data/providers/sportsradar.ts`, selected with `NFL_DATA_SOURCE=sportsradar` or `LIVE_DATA_SOURCE=sportsradar` | Keeps a working fallback for live scores and is the only source for transactions (api-sports has no transactions endpoint) |
| Identity for api-sports rows in the scraper | Reuse the existing lineage columns `DataSource="ApiSports"` + `DataSourceRecordId` with new indexes, instead of adding `ApiSportsId` columns next to `EspnId` | No new identity column per provider; `GameRepository.UpsertAsync` gets one extra match step |
| Injuries | New `InjuryReport` snapshot table (current status per player), separate from the per-game `Injuries` table | api-sports `/injuries` is "who is hurt right now", not per game, and keeps no history |

The long pole is **deploying the scraper API where Vercel can reach it** (today it only runs locally). Phase S5 below covers that. Until it is deployed, the chatbot can run one more provider (`api-sports` direct, Phase C1b, about 150 lines) against the same interface so the pick'em season is not blocked on the deployment. That is optional and shares the key quota, so switch it off once the scraper is up.

---

## 1. api-sports facts that shape the design

Verified from the vendor docs and beginner guide. Field names must be re-checked against real responses on Day 0 (see §5).

- **Auth:** `x-apisports-key: <key>`. GET only. Base URL `https://v1.american-football.api-sports.io`.
- **Quota:** free plan 100 requests/day, resets 00:00 UTC, 10 requests/minute. Pro (about $19/month) raises it to thousands/day. The plan below fits the free plan on a normal day but game-day score polling is the thing that will push you to Pro.
- **Endpoints used:**
  - `GET /teams?league=1&season=YYYY` – 32 teams, includes `code` (abbreviation). 1 call.
  - `GET /games?league=1&season=YYYY` – **the whole season's schedule and results in one call** (schedule + finals + quarter scores). This is the big win over SportsRadar's 18 week calls.
  - `GET /games?league=1&date=YYYY-MM-DD` or `GET /games?live=all` – game-day score polling.
  - `GET /games/statistics/players?id=<gameId>` – box score for one game (passing, rushing, receiving, defensive, kicking, punting, returns). 1 call per game, 16/week.
  - `GET /games/statistics/teams?id=<gameId>` – team totals per game.
  - `GET /players?team=<id>&season=YYYY` – roster. 32 calls per full pass.
  - `GET /injuries?team=<id>` (or `player=`) – current injuries. 32 calls per full pass. No `league` filter, no history.
  - `GET /standings?league=1&season=YYYY` – 1 call (optional; the chatbot already derives standings from finals).
  - `GET /odds?game=<id>` – optional, feeds the existing `GameOdds` table later.
- **Shapes to map:** `game.stage` is `"Pre Season" | "Regular Season" | "Post Season"`; `game.week` is a string (`"Week 4"`, `"Wild Card"`, `"Divisional Round"`, `"Conference Championships"`, `"Super Bowl"`); `game.date.timestamp` is a unix UTC timestamp; `game.status.short` is one of `NS, Q1, Q2, Q3, Q4, OT, HT, FT, AOT, CANC, PST`; scores are `scores.home.quarter_1..quarter_4, overtime, total`.
- **Gaps:** no transactions / roster-move feed (keep SportsRadar or Grok news for `getRosterMoves`); no injury history; `coverage.injuries` on `/leagues` must be checked rather than assumed.

### Daily request budget (free plan, scraper only)

| Job | Calls | When |
|-----|-------|------|
| Season schedule + results refresh | 1 | every 12h (existing `ScheduleRefreshScheduler`) |
| Game-day score poll | about 50 | Sun 1pm–midnight ET every 15 min; Thu/Mon evenings about 15 each |
| Player box scores for finished games | 16 | Tuesday |
| Current injuries, 32 teams | 32 | Wed and Fri |
| Rosters, 32 teams | 32 | Tuesday |
| Standings | 1 | daily |

Heaviest day is Sunday at about 55 calls. Tuesday is about 50. Everything is schedulable under 100 as long as only the scraper holds the key. If you want 5-minute live scores in chat, buy Pro; do not shorten the poll interval on the free plan.

---

## 2. Target architecture

```
api-sports.io ──► dotnet scraper (ApiSports provider, DataProvider="ApiSports")
                     │  SQLite locally / PostgreSQL in production
                     │  REST  /api/v1/schedule, /games, /players, /injuries/current  (X-Api-Key, read scope)
                     ▼
              Next.js chatbot
                ├─ lib/nfl-data/            provider interface (schedule, scores, injuries)
                │    ├─ providers/scraper-api.ts    ← default (NFL_DATA_SOURCE=scraper)
                │    ├─ providers/sportsradar.ts    ← moved from lib/sportsradar.ts (fallback)
                │    └─ providers/api-sports.ts     ← optional direct bridge until the scraper is deployed
                ├─ cron sync-schedule / sync-scores  → NflGame (unchanged downstream: pick week, locking, scoring, standings)
                └─ chat live tools (getLiveScores, getUpcomingSchedule, getInjuryReport, getRosterMoves)
```

Nothing in picks, bracket, leagues, Yards or standings changes. Only the *source* of `NflGame` rows and the four live tools changes.

---

## 3. Scraper plan (`dotnet-AI-football-scraper`)

### S1. ApiSports provider (follows the SportsData.io template exactly)

New folder `src/WebScraper.Core/Services/Scrapers/ApiSports/`:

| File | Purpose |
|------|---------|
| `ApiSportsDtos.cs` | `ApiSportsEnvelope<T>` (`get`, `parameters`, `errors`, `results`, `response`), team, game, player, player-statistics, team-statistics, injury, standings DTOs |
| `ApiSportsMappings.cs` | api-sports team id ↔ NFL abbreviation for all 32 teams (filled from the Day 0 `/teams` capture), `ParseWeek("Week 4" / "Wild Card" ...)` → `(NflSeasonType, int week)`, `ParseStatus("FT")` → `GameStatus` string + `IsFinal`, postseason week numbering matches `NflSeasonSchedule` (Wild Card=1, Divisional=2, Conference=3, Super Bowl=4) |
| `ApiSportsTeamService` | `GET /teams?league=1&season=` → `Team` upsert by abbreviation, stamps `DataSource/DataSourceRecordId` |
| `ApiSportsGameService` | `ScrapeGamesAsync(season, seasonType)` = one `GET /games?league=1&season=` call, filter by stage client-side; `ScrapeGamesAsync(season, week, seasonType)` = same call filtered to the week (api-sports has no week filter, and one call is cheaper than caching). Stores scheduled games with **null scores** when `status.short == "NS"` (same rule the ESPN service already follows), kickoff from `date.timestamp` as UTC, quarter scores, `GameStatus`, `HomeWinner`, venue name/city via `IVenueRepository` |
| `ApiSportsPlayerService` | `GET /players?team=&season=` per team → `Player` upsert by `(DataSource, DataSourceRecordId)` with `PlayerTeamSeason` rows |
| `ApiSportsStatsService` | for each final game in the week: `GET /games/statistics/players?id=` → `PlayerGameStats` (discovers players by api-sports id like the ESPN path does by athlete id), `GET /games/statistics/teams?id=` → `TeamGameStats` |
| `ApiSportsInjuryService` | new interface `IInjuryScraperService.ScrapeCurrentInjuriesAsync()`: `GET /injuries?team=` × 32 → `InjuryReport` snapshot (see S2). Registered only for providers that support it; `NoOpInjuryScraperService` otherwise (same pattern as `NoOpOddsPollService`) |
| `ApiSportsScorePollService` | `GET /games?league=1&date=<today UTC and ET>` → score-only upsert for games already in the DB. Used by the new game-day poller (S3) |

Wiring:
- `DataProvider` enum: add `ApiSports`. `DataProviderFactory.RegisterScrapers`: new `case "apisports"` using `AddApiHttpClient` with `AuthType="Header"`, `AuthHeaderName="x-apisports-key"`. `ConsoleDisplayService` provider list + display name ("API-Sports").
- `BaseApiService`: api-sports returns HTTP 200 with a populated `errors` object on bad key / quota exhaustion. Add an `ApiSportsEnvelope` check in the ApiSports services (not in the base class) that logs `errors` and returns `ScrapeResult.Failed` so a quota hit does not look like "0 games".
- `RequestDelayMs` for ApiSports: **6500** (10/minute cap). Polly retry already handles 429.
- `appsettings.json` (Cli and Api) `Providers.ApiSports` block: `BaseUrl`, `AuthType`, `AuthHeaderName`, `ApiKey: ""`, `RequestDelayMs: 6500`. Set `DataProvider` to `"ApiSports"` in `src/WebScraper.Api/appsettings.json` so `ScheduleRefreshScheduler` and `ScrapeJobWorker` use it (they resolve the single registered provider from DI and stamp `ScrapeJob.Source` from it).

### S2. Schema changes (one migration, `ApiSportsProvider`)

- Index `(DataSource, DataSourceRecordId)` on `Games` and `Players` so the upserts are not table scans.
- `GameRepository.UpsertAsync`: match order becomes `EspnEventId` → `(DataSource, DataSourceRecordId)` → natural key `(Season, SeasonType, Week, HomeTeamSeasonId, AwayTeamSeasonId)`. Natural key is what lets api-sports rows merge into games ESPN already loaded for 2026 instead of duplicating them. On update also copy `DataSource`, `DataSourceFetchedAt`, `DataSourceRecordId`.
- `PlayerRepository`: `UpsertByExternalIdAsync(source, recordId)` next to `UpsertByEspnIdAsync`.
- New entity `InjuryReport` (`Id`, `TeamSeasonId`, `PlayerId?`, `ExternalPlayerId`, `PlayerName`, `Position`, `Status`, `Description`, `ReportedAt`, `SnapshotAt` + auditable + soft-delete). Unique on `(ExternalPlayerId, SnapshotAt::date)`. Repository `IInjuryReportRepository` with `GetCurrentAsync(teamAbbr?)` returning the latest snapshot per player.
- `QualityRulesEngine` "missing EspnId" rule → "missing external id" (EspnId **or** `DataSourceRecordId`), otherwise every api-sports player becomes a finding.
- `DatabasePushService` stages: add `InjuryReports` after `Injuries`.

### S3. Schedulers and jobs

- `ScrapeJobType`: add `Injuries` and `ScorePoll`. `ScrapeController`: `POST /api/v1/scrape/injuries`, `POST /api/v1/scrape/score-poll`. `ScrapeJobWorker`: two new `Run*Async` branches. `NewScrape.razor` job-type select gets both.
- `ScheduleRefreshScheduler` stays as is (12h, one Games job = 1 api-sports call now).
- New `ScorePollScheduler` (hosted service, config section `ScorePoll`): `Enabled`, `IntervalMinutes` (15), `ActiveWindows` (default Thu 20:00–00:30 ET, Sun 13:00–00:30 ET, Mon 20:00–00:30 ET), plus "any day with a game whose kickoff is within the last 4h". Enqueues a `ScorePoll` job only when there is an unfinished game kicked off today, so an empty Sunday in the bye-heavy weeks costs nothing.
- New `InjuryRefreshScheduler` (config `InjuryRefresh`: `Enabled`, `Days` = Wed, Fri, `HourUtc`). Enqueues one `Injuries` job.
- Stats: reuse the existing dependency chain (stats job after games job succeeds); schedule a Tuesday `Stats` job for the just-finished week through `ScheduleRefreshScheduler` (`StatsAfterWeekEnds: true`).

### S4. API surface the chatbot will call

| Method | Route | Change |
|--------|-------|--------|
| GET | `/api/v1/schedule?season=&seasonType=&week=&team=` | exists; add `status` and `broadcastNetworks` to `GameDto` if missing, add `isFinal` convenience flag |
| GET | `/api/v1/schedule/upcoming?days=&team=` | exists |
| GET | `/api/v1/games?season=&week=` | exists |
| GET | `/api/v1/injuries/current?team=` | **new** (`InjuriesController`), read scope, returns latest snapshot grouped by team |
| GET | `/api/v1/status` | add `lastScorePollAt`, `lastInjurySnapshotAt`, `provider` so the chatbot can show freshness and the runbook can verify the crons |

Also: `GameDto.Meta.Source` already carries lineage, so the chatbot can label answers "via API-Sports".

MCP: `nfl_get_current_injuries` tool + README row (47 tools). Skill runbook: "Switch provider to ApiSports and re-sync the current season".

### S5. Deploy the API where Vercel can reach it (M5, minimum slice)

This is required for the chatbot to read from the scraper at all.
- `Dockerfile` for `WebScraper.Api` (multi-stage, `dotnet publish -c Release`), `docker-compose.yml` with PostgreSQL for local parity.
- `DatabaseProvider=PostgreSQL` in production, connection string + `ScraperSettings__Providers__ApiSports__ApiKey` + `Jwt__SigningKey` + `InitialAdmin__*` as environment variables (ASP.NET binds `__` to `:` automatically).
- Host on DigitalOcean App Platform (already the stated target) or Fly.io. Single instance only (`ScrapeJobWorker` and `ScrapeEventRelay` are single-instance by design).
- First boot: migrations run, `IdentitySeeder` creates the admin, log in at `/admin`, create a **read**-scope API key at `/admin/api-keys`. That key is `NFL_API_KEY` for the chatbot.
- Keep local SQLite + `push` for the historical backfill; the production API can also just scrape directly into PostgreSQL once it is up.

### S6. Tests

- `tests/WebScraper.Core.Tests/Scrapers/ApiSports/`: `ApiSportsMappingsTests` (32 teams, week/stage parsing, status mapping), `ApiSportsGameServiceTests` (fixture-driven: scheduled game stores null scores, final game stores totals + quarters, postponed game keeps id and moves week), `ApiSportsStatsServiceTests`, `ApiSportsInjuryServiceTests`, envelope-error test (quota exhausted → `ScrapeResult.Failed`).
- `DataProviderFactoryTests`: `apisports` registers the 4 scrapers + injury + score-poll services.
- Fixtures recorded on Day 0 go in `tests/WebScraper.Core.Tests/Fixtures/ApiSports/*.json`.

---

## 4. Chatbot plan (`nextjs-ai-football-chatbot`)

### C1. Provider interface and the scraper client

New `lib/nfl-data/`:

| File | Purpose |
|------|---------|
| `types.ts` | `NormalizedGame` (`sourceGameId`, `homeAbbrev`, `awayAbbrev`, `kickoff: Date`, `week`, `seasonType`, `status: "scheduled" \| "in_progress" \| "final" \| "postponed" \| "cancelled"`, `homeScore`, `awayScore`, quarters, `venue`, `broadcast`), `NormalizedInjury`, `NflDataProvider` interface: `getWeekSchedule(year, type, week)`, `getSeasonSchedule(year, type)`, `getCurrentInjuries(team?)`, `getTransactions?(year, month)`, `name`, `isConfigured()` |
| `providers/scraper-api.ts` | `fetch` to `${NFL_API_URL}/api/v1/...` with `X-Api-Key: NFL_API_KEY`, `next: { revalidate: 60 }` for live reads, maps `GameDto` → `NormalizedGame`. Handles the `PagedResult` envelope and the `error` envelope |
| `providers/sportsradar.ts` | today's `lib/sportsradar.ts` moved verbatim plus a `toNormalizedGame` adapter. `lib/sportsradar.ts` becomes a re-export so nothing else breaks during the move |
| `providers/api-sports.ts` (optional, C1b) | direct api-sports client with the same mappings as the scraper (`"Week 4"`, `status.short`, `date.timestamp`). Only for the window before S5 is deployed |
| `index.ts` | `getScheduleProvider()` / `getLiveProvider()` chosen by env: `NFL_DATA_SOURCE` = `scraper` (default when `NFL_API_URL` is set) \| `sportsradar` \| `api-sports`; `LIVE_DATA_SOURCE` defaults to `NFL_DATA_SOURCE`; transactions always fall back to SportsRadar when its key exists |

### C2. Schedule and score sync become source-agnostic

- `lib/db/seed/sync-schedule-helpers.ts`: `buildGameRow` takes a `NormalizedGame`; `SOURCE_PREFIX` becomes per-provider (`scraper:`, `sportradar:`, `apisports:`).
- **Migration gotcha:** the 2026 rows already in production carry `sportradar:<id>` source ids and the upsert conflicts on `sourceGameId`. Switching providers must **not** insert a second copy of the season. `upsertScheduleGame` gets a second lookup by `(seasonId, week, homeTeamId, awayTeamId)` when the source id is unknown, and rewrites `sourceGameId` to the new provider's id on that row. Covered by a unit test in `sync-schedule.test.ts` ("re-keys an existing SportsRadar game to the scraper id without duplicating"). Also add a `pnpm db:rekey-games --from=sportradar --to=scraper` dry-run script for the one-time switch, so the first sync is a visible operation rather than a surprise.
- `lib/db/sync-scores-helpers.ts`: `isSyncableFinalGame` works on `NormalizedGame.status === "final"` with both scores present. Postponed games are skipped, same as today. Quarter scores are written when present (the columns exist, nothing writes them today).
- `syncScheduleFromSportsRadar` → `syncSchedule(provider)`, `syncScoresFromSportsRadar` → `syncScores(provider)`; the cron routes and `pnpm db:sync-schedule` pass the configured provider. Window logic (`resolveScheduleSyncWindow`) is kept; when the provider is the scraper the window is irrelevant for cost (the scraper serves from its DB) so `--full` becomes the default for that provider.
- `vercel.json`: `sync-scores` can stay at `*/10` when reading from the scraper (no upstream quota is spent). Keep `sync-schedule` daily.

### C3. Live tools re-pointed

| Tool | New source | Fallback |
|------|-----------|----------|
| `getLiveScores` | scraper `GET /api/v1/schedule?season=&week=` (freshness = scraper's score poll, 15 min) | SportsRadar when `LIVE_DATA_SOURCE=sportsradar` |
| `getUpcomingSchedule` | scraper `GET /api/v1/schedule/upcoming?days=&team=` | SportsRadar |
| `getInjuryReport` | scraper `GET /api/v1/injuries/current?team=`; player search filters client-side | SportsRadar |
| `getRosterMoves` | **unchanged, SportsRadar** when its key exists; otherwise returns a structured "not available, use searchNFLNews" result and `conductorSystemPrompt` tells Haiku to use `searchNFLNews` for transactions | Grok news |

Each tool returns `source: provider.name` and, for the scraper, `asOf` from `Meta.UpdatedAt` so the conductor can say "as of 4:12 pm". Tool descriptions and `lib/ai/prompts.ts` lose the hard-coded "SportsRadar" wording ("live data provider" instead). `isSportsRadarConfigured()` checks become `provider.isConfigured()`.

### C4. Close the "no 2026 player stats" gap (follow-up, same plumbing)

New cron `app/(chat)/api/cron/sync-stats/route.ts` (Tuesdays): for each completed game of the last scored week, `GET /api/v1/games?season=&week=` then `GET /api/v1/games/{id}/player-stats`, upsert `NflPlayer` (keyed by a new `sourcePlayerId` column, migration `0026`) and `NflPlayerStats` keyed by `(playerId, gameId)`. This replaces the dead migration-`0011` copy and makes `getPlayerInfo` / `comparePlayers` answer about the current season. It is scoped separately because it needs the scraper's stats job (S1/S3) running first.

### C5. Docs and config

- `.env.example`: add `NFL_API_URL`, `NFL_API_KEY`, `NFL_DATA_SOURCE`, `LIVE_DATA_SOURCE`, optional `API_SPORTS_KEY`; keep `SPORTSRADAR_*` under a "fallback" comment.
- `CLAUDE.md`, `docs/go-live-runbook.md` (section 1 env table, section 4 budget → api-sports budget, section 5 table), `docs/ai-sdk.md`, `docs/api-routes.md`, `docs/prompts.md`: replace "SportsRadar" with "scraper API (api-sports upstream)" where it describes the primary path, and add the fallback note.
- Unit tests: `lib/nfl-data/providers/scraper-api.test.ts` (DTO → normalized mapping, error envelope), `sync-schedule.test.ts` re-key case, `sync-scores-helpers.test.ts` status mapping. Add them to `test:unit` in `package.json`.

---

## 5. Where you add the keys

| Key | Where | Name |
|-----|-------|------|
| **api-sports key** (scraper, local CLI) | `src/WebScraper.Cli/appsettings.Local.json` (git-ignored, create it if missing) | `ScraperSettings:Providers:ApiSports:ApiKey` |
| **api-sports key** (scraper, local API + admin dashboard) | `src/WebScraper.Api/appsettings.Local.json` | same path |
| **api-sports key** (scraper, hosted) | DigitalOcean / Docker environment variable | `ScraperSettings__Providers__ApiSports__ApiKey` |
| **Scraper read key** (chatbot) | Vercel → Project → Settings → Environment Variables | `NFL_API_URL` = `https://<scraper-host>`, `NFL_API_KEY` = key created at `/admin/api-keys` with scope `read` |
| Source switch (chatbot) | Vercel env | `NFL_DATA_SOURCE=scraper` (set `sportsradar` to roll back instantly, no deploy) |
| Optional direct bridge (chatbot, only until S5 is live) | Vercel env | `API_SPORTS_KEY`, `NFL_DATA_SOURCE=api-sports` |
| SportsRadar (kept) | leave `SPORTSRADAR_API_KEY` in place | fallback for live scores and the only source for `getRosterMoves` |

`appsettings.Local.json` shape for the scraper:

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
        "RequestDelayMs": 6500
      }
    }
  }
}
```

I will leave `ApiKey` empty in the checked-in `appsettings.json` and never read the key from chat; you paste it into the Local file or the host's env.

---

## 6. Order of work and rollout

**Day 0 – discovery (4 of the day's 100 requests, needs the key in the scraper's Local file).**
Record real responses to `tests/WebScraper.Core.Tests/Fixtures/ApiSports/`: `teams.json`, `games-season.json`, `injuries-team.json`, `game-player-stats.json`. Confirm the 32 `code` values, the `week` strings, the status codes, and whether `coverage.injuries` is true for the NFL. Every DTO and mapping test is written against these files, not against memory.

**Week 1 – scraper S1, S2, S6.** Provider + migration + tests. Run `games --season 2026 --source ApiSports` locally and diff against the ESPN rows already in SQLite (the natural-key merge must produce 272 games, not 544). CLAUDE.md provider table updated.

**Week 1 – chatbot C1, C2 (and C1b if you want the season unblocked before the scraper is hosted).** Provider interface, scraper client, source-agnostic sync, re-key script, tests. Deploy with `NFL_DATA_SOURCE=sportsradar` first (behavior identical), then flip.

**Week 2 – scraper S3, S4, S5.** Schedulers, injuries endpoint, Docker + hosted PostgreSQL, admin login, read key. Verify `/api/v1/status` shows `provider: ApiSports` and recent `lastScorePollAt`.

**Week 2 – chatbot C3, C5.** Live tools on the scraper, prompt wording, runbook. Flip `NFL_DATA_SOURCE=scraper` in Vercel, run `pnpm db:rekey-games` dry run then `--apply`, trigger `sync-schedule` and `sync-scores` once by hand and check the runbook's SQL (272 games, finals per week, 32 standings rows).

**Week 3 – C4 (player stats cron) and api-sports odds into `GameOdds`.** Optional, not needed for picks.

**Rollback at any point:** `NFL_DATA_SOURCE=sportsradar` in Vercel. The re-key is reversible with the same script (`--from=scraper --to=sportradar`) because the natural key still matches.

---

## 7. Risks and open questions

1. **Free-plan quota.** Score polling every 15 minutes on Sunday is the budget driver. If chat users expect near-live scores, plan on the Pro tier; nothing in the design changes, only `ScorePoll:IntervalMinutes`.
2. **Mixed-source history.** 2006–2025 stays ESPN in the scraper. api-sports only writes 2026 onward unless you choose to backfill, so `Players` will have both `EspnId` and api-sports `DataSourceRecordId` rows. Player identity across the two is by name + team-season until a crosswalk exists. Fine for the chatbot (it keys on its own `NflPlayer`), worth knowing for the quality rules.
3. **Transactions.** No api-sports source. `getRosterMoves` keeps SportsRadar; if that key dies the tool degrades to Grok news. Decide whether that is acceptable or whether roster moves should drop from the tool list.
4. **Team codes.** api-sports may use `WSH`/`JAX`-style codes like SportsRadar does. The mapping lives in one place (`ApiSportsMappings`) and the Day 0 capture settles it.
5. **Postseason.** api-sports returns the full season in one call including `Post Season`, so the bracket's playoff games can come from the same sync later (the chatbot's `gameType` enum already exists). Not in scope for the first cut.
6. **Scraper hosting cost.** One small App Platform instance plus a managed PostgreSQL. If you would rather not host yet, C1b (direct api-sports in the chatbot) carries the pick'em season alone, at the price of the chatbot holding the key and no 2026 player stats.
