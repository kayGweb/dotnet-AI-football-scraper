# Agent-Managed Football Database — Execution Plan

**Goals**
1. A database an agent can manage end-to-end (not just read).
2. 20 seasons of NFL data loaded before end of 2026.
3. All available football data by summer 2027.
4. An MCP server + a Skill so Claude can operate it.
5. A self-improving loop — the system finds and fills its own gaps.
6. A human login to check the state of the application.

---

## 0. The headline: the backfill is not the hard part

Runtime math for 20 seasons (2006–2025) from the ESPN provider:

| Work | Calls | At 1.5s delay |
|---|---|---|
| Scoreboard (20 seasons × ~22 weeks × 3 season types) | ~450 | 11 min |
| Game summaries (box scores) — ~5,430 games | 5,430 | 2h 15m |
| **Total** | **~5,900** | **~2.5 hours** |

Game count derivation: 2006–2019 = 256 reg + 11 playoff = 267/season (×14);
2020 = 256 + 13 = 269; 2021–2025 = 272 + 13 = 285/season (×5). Total 5,432.

**The 20-year load is one afternoon of wall-clock time.** The five months between
now and December are not fetch time — they are correctness time. Nothing in the
codebase today will produce a *correct* 20-year dataset, for the five reasons
below. Fix those and the load itself is a rounding error.

This reframes the schedule: front-load the identity/orchestration work, run the
backfill in November, leave December for reconciliation.

---

## 1. Five blockers found in the current code

### 1.1 Player identity is keyed on `Name + TeamId` — BLOCKER
`PlayerRepository.UpsertAsync` matches on `p.Name == player.Name && p.TeamId == player.TeamId`.

Over 20 seasons this fails three ways:
- **Fragmentation.** A player on 4 teams becomes 4 unrelated `Player` rows. Career
  totals become impossible without fuzzy re-joining after the fact.
- **Collision.** Two players with the same name on the same team merge into one row
  and their stats interleave. This is not hypothetical — the league has had
  concurrent same-name players (e.g. two Josh Allens, two Steve Smiths).
- **Churn.** `TeamId` is mutable current-team state on what should be an identity
  record. Every trade rewrites history.

`Player.EspnId` already exists and is the correct key — it is just not used as one.

**Fix:** rekey player upsert on source ID (`EspnId`, later `PfrId`), make `TeamId`
a *current team* convenience field, and move the player↔team↔season relationship
into a `PlayerTeamSeason` join table.

### 1.2 Only regular season is scraped — BLOCKER
`EspnGameService` hardcodes `seasontype=2` at both call sites (lines 63, 278).
`seasontype=1` is preseason, `3` is postseason.

Result: **every playoff game and every Super Bowl of the last 20 years is
missing** — roughly 250 games including the ones people actually ask about.

**Fix:** parameterize season type; add it to `ScrapeJob` so jobs are addressable
as (season, seasonType, week).

### 1.3 Rosters can only be fetched for *today* — ARCHITECTURAL
`EspnPlayerService` calls `/teams/{espnId}/roster`, which returns the **current**
roster. There is no `?season=` on that endpoint. You cannot retrieve the 2006
Falcons roster this way — running the players scraper 20 times gets you the 2026
roster 20 times.

**Fix:** historical players must be *discovered from box scores*. The stats scraper
already sees every athlete ID in `/summary`. Add a player-upsert path there so
scraping stats for 2006 creates the 2006 players as a side effect. Roster scraping
becomes a current-season enrichment step (height/weight/college), not the source
of player rows.

This inverts the current pipeline order for historical seasons:
`games → stats (creates players) → roster enrichment`, not `teams → players → games → stats`.

### 1.4 Teams are single mutable rows keyed on abbreviation — BLOCKER
`Team` has one row per franchise, upserted on `Abbreviation`. Over 20 years:

| Change | Season |
|---|---|
| Rams STL → LA | 2016 |
| Chargers SD → LAC | 2017 |
| Raiders OAK → LV | 2020 |
| Washington → Football Team → Commanders | 2020, 2022 |

A 2006 Rams game will attach to a team row that says "Los Angeles Rams." Every
historical query returns anachronistic names, and abbreviation-keyed upserts will
either collide or orphan.

**Fix:** introduce `Franchise` (stable identity) + `TeamSeason` (name, city, abbr,
conference, division *as of that season*). `Game` FKs point at `TeamSeason`.
This is the single largest schema change in the plan and it must land before the
backfill, not after.

### 1.5 Nothing orchestrates a backfill — BLOCKER
`ScrapeJob` is one job per (type, season, week), triggered manually via
`POST /api/v1/scrape/*` or the dashboard. A 20-season load is **~1,320 hand-triggered
jobs**. There is no fan-out, no dependency ordering, no resume-after-crash beyond
the single-job orphan recovery, and no way to ask "what am I missing?"

**Fix:** a `Backfill` job type that fans out into child jobs, plus a coverage model
(§4) so the system can compute its own remaining work.

---

## 2. What "managed by an agent" actually requires

Today the MCP server exposes **14 read-only tools**. An agent can answer questions
about the data but cannot operate the system: it cannot start a scrape, check a
job, find a gap, or repair a bad row.

Three tiers of capability, each gated differently:

| Tier | Examples | Auth | Approval |
|---|---|---|---|
| **Read** (exists) | list/get teams, players, games, stats | API key, `read` scope | none |
| **Operate** (new) | trigger scrape, check job, query coverage, retry failed job | API key, `operate` scope | none |
| **Mutate** (new, guarded) | correct a field, merge duplicate players, soft-delete a row | API key, `admin` scope | **human approval queue** |

**Recommendation: agents propose mutations, humans approve them.** A `DataCorrection`
table holds proposed changes (entity, field, old value, new value, rationale,
proposing agent, status). The agent writes proposals; the dashboard has an approve/
reject queue; an approved correction is applied by a worker and logged. This gives
you a real audit trail and means a confused agent cannot quietly corrupt 20 years
of data. Merges and deletes are always proposals. Straightforward re-scrapes of a
known-bad game are auto-approved — re-running a scraper is idempotent and safe.

### New MCP tools

**Operate**
- `nfl_trigger_scrape(type, season, seasonType, week, source)` → job id
- `nfl_get_job(jobId)` / `nfl_list_jobs(status)` — progress and errors
- `nfl_get_coverage(season?, seasonType?)` — what's loaded vs expected (§4)
- `nfl_find_gaps(limit)` — ranked list of missing/suspect data
- `nfl_retry_job(jobId)`

**Introspect** (so the agent can reason about the schema instead of guessing)
- `nfl_describe_schema(entity?)` — tables, columns, types, FKs, meaning
- `nfl_get_data_dictionary()` — what each stat column means and where it comes from
- `nfl_query_stats(...)` — parameterized aggregation over a fixed whitelist of
  dimensions/measures. **Not** free-text SQL.

**Propose**
- `nfl_propose_correction(entityType, id, field, newValue, rationale)`
- `nfl_list_corrections(status)`

On `nfl_query_stats` vs. raw SQL: raw SQL against the DB is the fastest way to give
an agent power and the fastest way to get table scans, lock contention, and
accidental writes. A parameterized aggregation tool covers ~90% of real questions
with a bounded blast radius. If it proves too restrictive, the escape hatch is a
**read-only replica connection with a statement timeout**, never the primary.

---

## 3. The Skill

`skills/nfl-db/SKILL.md` does not exist yet (UpdatePlan_v1.md references it as if
it does). The Skill is what turns 20-odd tools into competent behavior. It should
encode:

- **Entity resolution first.** Never guess a player ID; resolve by name → ID, and
  when ambiguous, ask. (Depends on the search work in UpdatePlan_v1 W1.)
- **Season/week addressing.** How to interpret "week 3 of last year," when
  `seasonType` matters, why "the playoffs" is `seasonType=3`.
- **Coverage awareness.** Before answering "who led the league in 2009," call
  `nfl_get_coverage(2009)` — if 2009 is 60% loaded, say so rather than returning a
  confidently wrong leaderboard. **This is the single most valuable rule in the Skill.**
- **Operational runbooks.** How to backfill a season; how to diagnose a failed job;
  what to do when a game has no box score.
- **Mutation etiquette.** Propose, never assert. Include a rationale and a source.

Ship it versioned in-repo, and consider `GET /api/v1/skill` so remote clients pull
the current version.

---

## 4. Coverage & self-improvement

"Improves on itself" needs a concrete, safe definition. Here it means: **the system
knows what complete looks like, measures its distance from it, and files its own
work orders.** Not an agent rewriting its own source unsupervised.

### 4.1 Expected-vs-actual coverage
A `SeasonCoverage` table (or computed view) holds, per (season, seasonType, week):
expected game count, actual game count, games with box scores, games with team
stats, games with injuries, player rows, last verified timestamp.

Expected counts are derivable from league structure (teams × games ÷ 2, playoff
bracket size), which changes by era — encode it as a small table of league-era
rules, not hardcoded constants.

### 4.2 Quality rules
Cheap assertions that run after every scrape and produce `DataQualityFinding` rows:
- Game has a final status but no `PlayerGameStats` rows
- Sum of quarter scores ≠ final score
- Team game stats missing for one side
- Player with stats but no `Player` row / no `EspnId`
- Passing yards in a game > 600 (implausible → likely parse error)
- Duplicate players: same normalized name + same college + overlapping seasons
- Venue with no city/state
- A `TeamSeason` whose game count ≠ league schedule for that era

### 4.3 The loop
```
scrape → quality rules → findings → ranked gap list
   ↑                                      ↓
   └──── auto-enqueued repair jobs ───────┘
                (idempotent re-scrapes)
```
Findings that map to "re-scrape this game" auto-enqueue. Findings that need a
judgment call (merge these two players?) become correction proposals for the
approval queue. Everything is visible on the dashboard.

### 4.4 Cross-provider reconciliation
You have five providers. Once two of them cover the same game, disagreement is
signal. A `ReconciliationRun` compares ESPN vs PFR on a sample of games and files
findings on mismatch. This is how you catch systematic parse bugs that quality
rules miss — and it's the strongest argument for keeping the multi-provider
architecture rather than collapsing to ESPN-only.

---

## 5. Data we are not capturing

Assessed against what ESPN and PFR actually expose.

### Tier 1 — high value, available now, moderate effort
| Data | Source | Why it matters |
|---|---|---|
| **Playoff & preseason games** | ESPN `seasontype=1,3` | Currently 100% missing |
| **Drives** | ESPN `/summary` → `drives` | Scoring context, red-zone analysis |
| **Scoring plays** | ESPN `/summary` → `scoringPlays` | "How did they score?" — very common question |
| **Weather** | ESPN `/summary` → `gameInfo.weather` | Game-day conditions, already in a DTO you parse |
| **Officials / referee crew** | ESPN `/summary` → `gameInfo.officials` | Penalty-tendency analysis |
| **Game odds / betting lines** | ESPN `/summary` → `pickcenter` | Heavily requested; spread, O/U, moneyline |
| **Broadcast + kickoff time** | ESPN scoreboard `competitions.broadcasts` | "Gameday" completeness |
| **Player headshots / team logos** | ESPN CDN | Already scoped in UpdatePlan_v1 W2 |

### Tier 2 — high value, more work
| Data | Source | Notes |
|---|---|---|
| **Play-by-play** | ESPN `/playbyplay` | ~150–180 plays/game. 5,400 games ≈ 950k rows for 20 yrs. Own table, own job type. Not available pre-~1999 anywhere reliable. |
| **Snap counts** | PFR | Only PFR has these; 2012+ only |
| **Advanced/Next Gen stats** | PFR, NGS | 2016+ |
| **Coaches (head + coordinators)** | PFR | Per team-season |
| **Draft picks** | PFR | Complete back to 1936 |
| **Transactions** | PFR / NFL.com | Signings, cuts, trades |
| **Awards / Pro Bowl / HOF** | PFR | Cheap, high query value |
| **Standings by week** | ESPN | Derivable but expensive to compute repeatedly |

### Tier 3 — nice to have
Contracts/salary cap (OverTheCap, scraping ToS needs checking), combine results,
college stats, depth charts, uniform/jersey history, Madden ratings, media/video
(UpdatePlan_v1 W5).

### On venues specifically
`Venue` is currently one row per stadium keyed on `EspnId`. Over 20 years stadiums
get renamed (sponsorship), teams change buildings, and surface types change. Add
`VenueSeason` or at minimum `NameHistory` + `SurfaceType` + `Capacity` +
`Elevation` + `OpenedYear`/`ClosedYear`, and lat/long for weather joins.

---

## 6. Human login — state of the application

M4 already delivers most of this: cookie auth at `/admin/login`, role-gated pages,
dashboard with entity counts, jobs table, API usage analytics. What it does not yet
answer is *"is my data any good?"* Additions:

- **Coverage page** — a 20-year × 22-week heat map, green/amber/red per week.
  This is the single screen that tells you whether you'll hit the December goal.
- **Backfill control** — start/pause/resume a backfill; ETA; live progress.
- **Data quality page** — open findings ranked by severity, with a "fix it" button
  that enqueues the repair job.
- **Correction approval queue** — agent-proposed changes, diff view, approve/reject.
- **Agent activity log** — which agent called what, when. `ApiQueryLogs` already
  captures the raw data; this is a view over it.
- **Live job progress** — the SignalR hub (M3c) exists but the Jobs page still
  polls every 5s. Wire the hub for the backfill page, where 1,300 jobs make
  polling genuinely inadequate.

---

## 7. Phasing against the deadlines

### Phase A — Identity & schema (Aug 2026, ~3 weeks)
Blocks everything. Do not start the backfill before this lands.
1. `Franchise` + `TeamSeason`; migrate `Game` FKs; backfill from existing rows.
2. Rekey `Player` on `EspnId`; add `PlayerTeamSeason`; dedupe existing rows.
3. Parameterize `seasonType` through scrapers, `ScrapeJob`, and the API.
4. Player-upsert-from-boxscore path in `EspnStatsService`.
5. Era-aware league structure table (expected game counts).
6. Migrations + backfill scripts for existing data.

### Phase B — Orchestration & coverage (Sep 2026, ~3 weeks)
1. `Backfill` job type with fan-out, dependency ordering, resume.
2. `SeasonCoverage` + expected-vs-actual computation.
3. Quality rules engine + `DataQualityFinding`.
4. Auto-enqueue repair jobs from findings.
5. Coverage + quality dashboard pages.

### Phase C — Agent surface (Oct 2026, ~3 weeks)
1. Operate-tier MCP tools + `operate` scope.
2. Introspection tools (`describe_schema`, `data_dictionary`, `query_stats`).
3. `DataCorrection` proposal flow + approval queue UI.
4. **Write the Skill.** Ship versioned in-repo.
5. End-to-end test: an agent backfills a season unattended and reports coverage.

### Phase D — Tier 1 data expansion (Oct–Nov 2026, overlaps C)
Drives, scoring plays, weather, officials, odds, broadcast. Each is a
parse-and-store addition to the existing `/summary` handling — cheap, because you
are already fetching that JSON.

### Phase E — The 20-year backfill (Nov 2026)
Run it in **reverse chronological order** (2025 → 2006). Recent seasons have the
best data quality and the highest query value, so if anything goes wrong you have
already banked the seasons people actually ask about. Budget one week of
supervised running, not one afternoon, because the first three seasons will surface
parse bugs the quality rules then catch for the remaining seventeen.

**Milestone: 20 seasons loaded + coverage green — target Dec 1, 2026.**
December is reconciliation, cross-provider checks, and closing findings. This is
the buffer that makes the deadline credible.

### Phase F — Play-by-play (Dec 2026 – Feb 2027)
~950k rows for 20 seasons. Separate table, separate job type, run after the core
data is green.

### Phase G — Deep history, 1920–2005 (Feb–Jun 2027)
This is a genuinely different project and needs to be scoped as one:
- **ESPN box score coverage degrades before ~2002** and is unreliable before ~1994.
  Pre-2002 must come from **PFR HTML**, which means fragile parsers and a much
  more aggressive politeness budget (PFR rate-limits hard — assume 5–6s/request).
- Volume: ~14,000 games 1920–2005. At 5s ≈ 20 hours of fetch, spread over weeks.
- Identity resolution across 100 years is the real cost: franchise mergers
  (1943 "Steagles"), defunct teams, the AFL/NFL merger, name normalization.
- Play-by-play does not exist before ~1999. Set expectations: pre-1999 is
  scores + box scores, not plays.

**Milestone: all reliably-available football data — target Jun 2027.**
"All football data" should be defined as: **every game, team, and player-game
stat line that a public source actually exposes**, with a documented coverage
matrix showing what exists per era. Anything stronger is not achievable, and
saying so now is better than discovering it in May.

---

## 8. Decisions I need from you

1. **Agent write access** — proposal queue (recommended) vs. direct writes for
   scoped operations?
2. **Raw SQL for agents** — parameterized `query_stats` only (recommended), or
   read-only replica with free SQL as an escape hatch?
3. **Play-by-play** — in scope for the December milestone, or Phase F as scoped?
   It roughly doubles storage and adds a season's worth of parse surface.
4. **Betting odds** — include? It's the most-requested category and it's already
   in the `/summary` payload you fetch, but it changes the character of the product.
5. **Hosting for the backfill** — a 20-season run needs a machine that stays up for
   days. DigitalOcean App Platform (per CLAUDE.md) or something you control?
6. **Deep-history ambition** — full 1920+ (Phase G as written), or stop at 1970
   (merger era, far better data quality, ~60% less parser work)?

## 9. Environment note
This container has no .NET SDK (`dotnet: command not found`), so nothing here has
been built or tested. Migrations for `AuditableAndSoftDelete`, `ApiKeysTable`,
`ScrapeEventsTable`, and `InitialIdentity` all exist on disk — CLAUDE.md still
lists them as pending and should be corrected.
