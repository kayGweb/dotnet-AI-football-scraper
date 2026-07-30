# Agent-Managed Football Database — Execution Plan

**Goals**
1. A database an agent can manage end-to-end (not just read).
2. 20 seasons of NFL data loaded before end of 2026.
3. All available football data back to the 1970 merger by summer 2027.
4. An MCP server + a Skill so Claude can operate it.
5. A self-improving loop — the system finds and fills its own gaps.
6. A human login to check the state of the application.

**Decisions locked (2026-07-29)**

| # | Decision | Consequence |
|---|---|---|
| 1 | Agent writes go through a **proposal queue** | `DataCorrection` table + approval UI (§2, §6) |
| 2 | **Parameterized `query_stats` only** — no raw SQL | No read-only replica needed; whitelist is the contract (§2) |
| 3 | Play-by-play — *unanswered*, defaulted to **Phase F** | Not in the December milestone (§7) |
| 4 | **Include betting odds** | Promoted to a first-class modeled entity (§5.1) |
| 5 | Backfill runs on the **local Hermes agent** | Changes the run/delivery architecture (§7, Phase E) |
| 6 | Deep history **stops at 1970** (merger era) | Phase G rescoped: ~8,400 games, not ~14,000 (§7, Phase G) |

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

### 1.2b Player resolution is team-agnostic and fails silently — BLOCKER (worse than first assessed)

Verified in `EspnStatsService`. The stats pipeline:

1. Accumulates parsed stats into `Dictionary<string, PlayerGameStats>` **keyed on
   player display name** (line 125, threaded through `ParseCategory` at line 422).
2. Resolves each name via `_playerRepository.GetByNameAsync(playerName)` (line 151),
   which is `FirstOrDefaultAsync(p => p.Name == name)` — **exact string match,
   ignoring team entirely**.
3. On a miss: `LogDebug(...)` then `continue` (lines 153–156) — **the stat line is
   silently discarded at Debug level**.

Three consequences:

- **A historical stats scrape will report success having stored nothing.** Players
  from 2006 aren't in the DB (§1.3), so every line is dropped, `count` stays 0, and
  the job completes as `Succeeded` with `RecordsProcessed = 0`. At default log
  levels there is no visible warning. **The backfill would appear to work and
  produce an empty stats table.** Quality rules (§4.2) are the only thing that
  would catch this — which is why they must exist *before* Phase E, not after.
- **It corrupts current data too.** Because lookup is name-only, and §1.1's
  `Name + TeamId` upsert creates one row per team a player has played for, stats
  attach to whichever duplicate `FirstOrDefaultAsync` happens to return. Two
  same-name players collapse into one. This is live today, not a historical-only
  concern.
- **The ESPN athlete ID is already available and thrown away.**
  `EspnStatAthleteInfo.Id` is populated in the DTO but discarded because the
  dictionary is keyed on name.

**Fix (larger than originally scoped):** rekey the accumulator from name to ESPN
athlete ID. This touches `ParseCategory` and all ten category parsers, so it is a
refactor of the stats parse layer rather than a one-line lookup change. Raise the
unresolved-player log from Debug to Warning and surface the count in
`ScrapeResult.RecordsFailed` so a silent zero becomes impossible.

Injury parsing (line 348) has the same name-lookup pattern and the same fix, and it
already has `entry.Athlete.Id` in hand.

### 1.3 Rosters can only be fetched for *today* — ARCHITECTURAL
`EspnPlayerService` calls `/teams/{espnId}/roster`, which returns the **current**
roster. There is no `?season=` on that endpoint. You cannot retrieve the 2006
Falcons roster this way — running the players scraper 20 times gets you the 2026
roster 20 times.

**Fix:** historical players must be *discovered from box scores*. The stats scraper
already receives every athlete ID in `/summary` (`EspnStatAthleteInfo.Id`). Add a
player-upsert path there so scraping stats for 2006 creates the 2006 players as a
side effect — this depends on the §1.2b rekey landing first. Roster scraping
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

**DECIDED: agents propose mutations, humans approve them.** A `DataCorrection`
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

**DECIDED: parameterized `nfl_query_stats` only — no raw SQL, no replica.** Raw SQL
is the fastest way to give an agent power and the fastest way to get table scans,
lock contention, and accidental writes. A parameterized aggregation tool covers
~90% of real questions with a bounded blast radius.

Because the whitelist *is* the contract, it has to be designed rather than grown
ad hoc. Initial surface:

- **Dimensions:** season, seasonType, week, team, franchise, player, position,
  venue, conference, division, homeAway, opponent
- **Measures:** every numeric column on `PlayerGameStats` and `TeamGameStats`,
  plus game-level scores
- **Aggregations:** sum, avg, min, max, count, rank
- **Modifiers:** filter (=, !=, >, <, between, in), groupBy (≤3 dimensions),
  orderBy, limit (hard cap 500), having

Every call is logged to `ApiQueryLogs` with its parsed shape. **Review those logs
monthly** — the queries agents *try* and fail to express are the roadmap for
extending the whitelist. If a real question can't be expressed after two rounds of
extension, that's the signal to revisit the no-SQL decision, not a reason to hedge
now.

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
| **Officials / referee crew** | ESPN `/summary` → `gameInfo.officials` | **DTO already exists and parses** (`EspnOfficial`, `EspnGameInfo.Officials`) — nothing reads it. Needs only an entity + persistence, no parse work |
| **Betting odds** | ESPN `/summary` → `pickcenter` | **DECIDED: in scope.** First-class entity — see §5.1 |
| **Broadcast + kickoff time** | ESPN scoreboard `competitions.broadcasts` | "Gameday" completeness |
| **Player headshots / team logos** | ESPN CDN | Already scoped in UpdatePlan_v1 W2 |

### 5.1 Betting odds — modeled, not a column

Odds are not one value per game. They are *many values per game, per sportsbook,
over time*. Flattening them into three columns on `Game` throws away the thing
that makes odds interesting (line movement) and is unmigratable later.

**`GameOdds` table**
- `Id`, `GameId` (FK), `Sportsbook` (ESPN returns several providers)
- `Spread` (home-relative, signed), `OverUnder`, `HomeMoneyline`, `AwayMoneyline`
- `SnapshotType` (enum: Opening, Current, Closing)
- `CapturedAt` (UTC) — when *we* observed it
- Standard audit + soft-delete columns
- Unique index on `(GameId, Sportsbook, SnapshotType, CapturedAt)`

Opening and closing lines are the two rows that matter for analysis. Intraday
movement is optional and only obtainable going forward — see the coverage caveat.

**Three consequences you should expect:**

1. **Historical odds coverage is thin and we will not know how thin until we
   look.** ESPN's `pickcenter` block is populated for recent seasons but degrades
   going back, and is very likely absent for most of 2006–2012. Odds coverage
   therefore needs its **own row in the coverage matrix**, tracked separately from
   game/stats coverage, so "we have 2008" never implies "we have 2008 odds."
   Expect to fill historical gaps from a secondary source later; design for it now
   by keying on `Sportsbook` rather than assuming ESPN.

2. **Closing lines can only be captured live.** Once a game is final, ESPN reports
   whatever the last-known line was — you cannot reconstruct the true opening line
   after the fact. To get real opening/closing pairs for the 2026 season onward,
   an `OddsPoll` job must run on upcoming games (daily is enough; hourly in the
   24h before kickoff). **Start this in September 2026 regardless of where the rest
   of the plan is** — every week it isn't running is a week of lines lost forever.

3. **It changes the product's character, as flagged.** Once odds are in, the
   obvious next questions are against-the-spread records and over/under trends.
   Those are cheap to compute *if* `GameOdds` is modeled as above and expensive to
   retrofit if it isn't. No gambling-advice framing in the Skill — the Skill
   reports historical lines and results as data, and does not predict.

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
0. **Fixture-based tests for `EspnStatsService`** — it currently has none (§8b.5)
   and steps 2/4 refactor it. Do this first so the refactor has a safety net.
1. `Franchise` + `TeamSeason`; migrate `Game` FKs; backfill from existing rows.
   Start from the existing `NflTeams.cs` canonical table (§8b.6) and add era-awareness.
2. **Rekey the stats parse layer from player name to ESPN athlete ID** (§1.2b) —
   `ParseCategory` + all 10 category parsers + injury parsing. Raise unresolved-player
   logging to Warning and count it into `ScrapeResult.RecordsFailed`.
3. Rekey `Player` upsert on `EspnId`; add `PlayerTeamSeason`; dedupe existing rows.
4. Parameterize `seasonType` through scrapers, `ScrapeJob`, and the API.
5. Player-upsert-from-boxscore path in `EspnStatsService` (depends on step 2).
6. Era-aware league structure table (expected game counts).
7. Migrations + backfill scripts for existing data.

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

**DECIDED: the backfill runs on the local Hermes agent, not App Platform.** Good
call — this is a batch job, not a service, and paying for cloud uptime to run it
would be waste. It does change four things:

1. **Local SQLite is the write target; Postgres is the publish target.** The
   backfill writes to `data/nfl_data.db`, and the existing `DatabasePushService`
   promotes it. That's the right split, but push is currently an all-or-nothing
   full-table upsert — at ~5,400 games plus stats it needs **batching and
   resumability** before it's the delivery path for a dataset this size. Treat
   "make push incremental" as a Phase B deliverable, not a Phase E surprise.

2. **The machine will sleep, reboot, and lose Wi-Fi.** Assume interruption as the
   normal case. The `Backfill` job's checkpoint/resume (Phase B) is what makes
   this survivable, and it is now load-bearing rather than a nicety. Resume must
   work from a cold start with no in-memory state.

3. **Back up the SQLite file before each backfill session.** A corrupted local DB
   with no replica is the one failure mode here that costs real time. Cheap
   insurance: copy the file, keep the last three.

4. **Nothing needs to be internet-reachable during the backfill.** The API,
   dashboard, and MCP server can all point at the same local SQLite while it runs
   — you get live coverage monitoring with zero hosting. Deploy to App Platform
   when you want the data *served*, which is a separate decision from where it's
   *built*.

**Milestone: 20 seasons loaded + coverage green — target Dec 1, 2026.**
December is reconciliation, cross-provider checks, and closing findings. This is
the buffer that makes the deadline credible.

### Phase F — Play-by-play (Dec 2026 – Feb 2027)
~950k rows for 20 seasons. Separate table, separate job type, run after the core
data is green.

### Phase G — Deep history, 1970–2005 (Feb–Jun 2027)

**DECIDED: stop at 1970.** The right cutoff, and for a better reason than volume.
1970 is the AFL–NFL merger: it is the first season with one league, one set of
rules, one statistical standard, and a franchise set that mostly still exists.
Everything before it requires modeling defunct leagues, defunct teams, and
wartime franchise mergers (the 1943 "Steagles") for data almost nobody queries.

**Revised volume: ~8,400 games, 1970–2005.**

| Era | Teams | Games/season | Seasons | Games |
|---|---|---|---|---|
| 1970–1975 | 26 | 182 + 7 playoff | 6 | 1,134 |
| 1976–1977 | 28 | 196 + 7 | 2 | 406 |
| 1978–1989 | 28 | 224 + 10 | 12 | 2,808 |
| 1990–1994 | 28 | 224 + 11 | 5 | 1,175 |
| 1995–1998 | 30 | 240 + 11 | 4 | 1,004 |
| 1999–2001 | 31 | 248 + 11 | 3 | 777 |
| 2002–2005 | 32 | 256 + 11 | 4 | 1,068 |
| **Total** | | | **36** | **~8,372** |

Combined with Phase E, the finished dataset is **56 seasons, ~13,800 games**.

At a PFR-polite 5s/request that's ~12 hours of fetch — again, not the constraint.
The constraints are:

- **ESPN box score coverage degrades before ~2002** and is unreliable before
  ~1994. Pre-2002 comes from **PFR HTML**: fragile parsers, and PFR rate-limits
  hard. Budget 5–6s/request and expect to be throttled anyway.
- **Stat categories are not constant across eras.** Sacks are not official before
  1982. Targets don't exist before 1992. QBR is 2006+. The schema must permit
  nulls and the coverage matrix must record *which categories exist per era* —
  otherwise every historical leaderboard is silently wrong. This is the single
  biggest correctness risk in Phase G.
- **Franchise identity still moves after 1970**, just less chaotically: Colts
  BAL→IND (1984), Cardinals STL→PHX→ARI, Raiders OAK→LA→OAK→LV, Rams LA→STL→LA,
  Oilers→Titans (1997–99). The `Franchise`/`TeamSeason` model from Phase A handles
  all of these.
- **The Browns/Ravens case needs an explicit ruling.** In 1996 the Cleveland
  franchise physically moved to Baltimore and became the Ravens, but the NFL
  treats the Browns' records and identity as having *stayed in Cleveland*, with
  the 1999 expansion team continuing them. So the legal franchise and the
  record-keeping franchise diverge. **Recommendation: follow the NFL's convention**
  — Browns history is continuous 1946→1995, dormant 1996–1998, resumes 1999;
  Ravens are a new franchise starting 1996. Encode it as data in the `Franchise`
  table, not as a special case in code.
- **Play-by-play does not exist before ~1999.** Pre-1999 is scores + box scores,
  and that should be stated in the coverage matrix rather than discovered.

**Milestone: all reliably-available football data back to 1970 — target Jun 2027.**
Definition of done: **every game, team, and player-game stat line from 1970 onward
that a public source actually exposes**, plus a published coverage matrix showing
what exists per era and per category. Anything stronger isn't achievable, and
saying so now beats discovering it in May.

---

## 8. Decisions

Five of six resolved 2026-07-29 — see the table at the top. Consequences are
folded into §2 (proposal queue, query whitelist), §5.1 (odds), and §7 (local
backfill, 1970 cutoff).

### Still open

**Play-by-play in the December milestone, or Phase F?** Defaulted to **Phase F**
(after the December milestone) so the plan can proceed. Reasons to leave it there:
it's ~950k rows for 20 seasons, roughly doubles storage, and adds a large new
parse surface right when Phase E needs attention on core correctness. Reasons to
pull it forward: it's the richest data ESPN exposes, and re-walking 5,400 games
later costs another full backfill pass.

Answer any time before **November**; after the Phase E run starts, pulling it
forward means a second pass over every game.

### Two new items that surfaced from these answers

- **Start the `OddsPoll` job in September 2026**, ahead of its phase. Closing lines
  can't be reconstructed after kickoff (§5.1). Every week it isn't running is a
  week of 2026 lines permanently lost.
- **Make `DatabasePushService` incremental and resumable in Phase B.** With the
  backfill running locally, push is now the only delivery path for ~5,400 games of
  data, and it's currently an all-or-nothing full-table upsert (§7 Phase E).

## 8b. Verification pass (2026-07-29)

Every claim in §1 and §5 was checked against the code. Results:

### Confirmed as written
| Claim | Evidence |
|---|---|
| Player upsert keyed on `Name + TeamId` | `PlayerRepository.UpsertAsync` |
| Team upsert keyed on `Abbreviation` only | `TeamRepository.UpsertAsync` → `GetByAbbreviationAsync` |
| `seasontype=2` hardcoded | `EspnGameService.cs:63` and `:278` |
| Roster endpoint is current-only | `EspnPlayerService.cs:70` — `/teams/{id}/roster`, no season param |
| No backfill orchestration | `ScrapeJobType` = Teams/Players/Games/Stats/All only |
| MCP is read-only | 14 tools, all `nfl_list_*` / `nfl_get_*` |
| No Skill in repo | no `SKILL.md` anywhere |
| `Venue` is minimal | no capacity, surface type, lat/long, opened/closed year |
| Migrations all present | 6 in Core + `InitialIdentity` in Api |

### Corrections to the plan
1. **§1.2b added** — player resolution is name-only and drops silently. Materially
   worse than the original §1.3 framing, and the fix is a parse-layer refactor
   rather than an added upsert path. **This is now the highest-priority fix in
   Phase A.**
2. **Officials are cheaper than stated** — the DTO exists and already parses; only
   the entity and persistence are missing. Moved from "parse and store" to
   "store only."
3. **Weather, odds, drives, scoring plays, and broadcast have no DTOs.** Confirmed
   absent from `EspnDtos.cs`. These are genuinely parse-and-store, as §5 says — the
   fields are in the HTTP response, not in our object model.
4. **`DatabasePushService` is worse than "all-or-nothing"** — it calls
   `.ToListAsync()` on every table (`localStats` at line 300) and builds in-memory
   `playerIdMap`/`gameIdMap` dictionaries. At 20 seasons that's ~270k stat rows
   with ~40 columns materialized at once, plus no resume. Reinforces moving
   incremental push into Phase B.

### New findings not previously in the plan
5. **`EspnStatsService` has zero test coverage.** `tests/.../Scrapers/Espn/` holds
   tests for Mappings, TeamService, and GameService only. The most complex scraper
   in the codebase — 10 stat categories, ~40 columns, plus team stats, venues,
   injuries, and API links — is untested, and the entire backfill depends on it.
   **Add fixture-based tests for it in Phase A**, before the parse-layer refactor,
   so the refactor has a safety net. This is the cheapest risk reduction available.
6. **`NflTeams.cs` already exists** — a canonical static table of all 32
   abbreviations with conference and division, explicitly intended as the single
   source of truth for provider mappings. It's the right starting point for the
   `Franchise`/`TeamSeason` work in Phase A, but note it encodes only the *current*
   era and is exactly what needs era-awareness added.
7. **Migration filename is misleading** (cosmetic, no action required):
   `20260531231235_ScrapeEventsTable.cs` actually creates the **ScrapeJobs** table;
   `20260604005928_AddScrapeEventsTable.cs` creates ScrapeEvents. Both tables are
   created correctly and there is no conflict — only the name is wrong.

### Not verifiable in this environment
No .NET SDK, so nothing was compiled or executed. Everything above is static
reading. Runtime behavior of the ESPN provider against live endpoints — especially
`pickcenter` availability per season (§5.1) — remains unverified and should be
spot-checked before Phase D is scheduled.

## 9. Environment note
This container has no .NET SDK (`dotnet: command not found`), so nothing here has
been built or tested. Migrations for `AuditableAndSoftDelete`, `ApiKeysTable`,
`ScrapeEventsTable`, and `InitialIdentity` all exist on disk — CLAUDE.md still
lists them as pending and should be corrected.
