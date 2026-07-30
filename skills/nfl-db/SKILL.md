# NFL Database Agent Skill

Version: 1.0.0

## Core rules

1. **Entity resolution first.** Never guess a player or team ID. Resolve by name → ID via `nfl_list_players` or `nfl_get_team_by_abbreviation`. When ambiguous, ask the user.

2. **Coverage awareness.** Before answering league-wide questions (e.g. "who led the league in passing yards in 2009"), call `nfl_get_coverage(season=2009)`. If coverage is partial, state that explicitly — do not return a confidently wrong leaderboard.

3. **Season/week addressing.**
   - `seasonType=1` preseason, `2` regular, `3` postseason (playoffs + Super Bowl).
   - "The playoffs" means `seasonType=3`. Super Bowl is postseason week 4.
   - Regular season weeks: 17 (2006–2020) or 18 (2021+).

4. **Mutation etiquette.** Never write data directly. Use `nfl_propose_correction` with a clear rationale and source. Humans approve corrections in the dashboard.

5. **Operational safety.** Re-scrapes (`nfl_trigger_scrape`, `nfl_retry_job`) are idempotent. Prefer re-scrape over manual field edits for parse errors.

## Tool tiers

| Tier | Tools | Scope |
|------|-------|-------|
| Read | `nfl_list_*`, `nfl_get_*`, `nfl_get_status`, `nfl_describe_schema`, `nfl_get_data_dictionary`, `nfl_query_stats` | `read` |
| Read (game detail) | `nfl_get_game_drives`, `nfl_get_game_scoring_plays`, `nfl_get_game_weather`, `nfl_get_game_officials`, `nfl_get_game_odds` | `read` |
| Operate | `nfl_trigger_scrape`, `nfl_get_job`, `nfl_list_jobs`, `nfl_get_coverage`, `nfl_find_gaps`, `nfl_retry_job` | `operate` |
| Backfill | `nfl_estimate_backfill`, `nfl_start_backfill`, `nfl_get_backfill_progress`, `nfl_pause_backfill`, `nfl_resume_backfill` | `operate` |
| Quality | `nfl_get_quality_findings`, `nfl_scan_quality`, `nfl_repair_finding`, `nfl_enqueue_repairs`, `nfl_refresh_coverage` | `operate` |
| Publish | `nfl_get_push_status`, `nfl_trigger_push`, `nfl_backup_database` | `admin` |
| Propose | `nfl_propose_correction`, `nfl_list_corrections` | `admin` |

Read tier in full: `nfl_list_teams`, `nfl_list_players`, `nfl_list_games`, `nfl_list_venues`,
`nfl_get_team`, `nfl_get_team_by_abbreviation`, `nfl_get_player`, `nfl_get_player_stats`,
`nfl_get_game`, `nfl_get_game_team_stats`, `nfl_get_game_player_stats`, `nfl_get_game_injuries`,
`nfl_get_venue`, `nfl_get_status`.

**Starting a backfill:** prefer `nfl_start_backfill` over `nfl_trigger_scrape(type=backfill)`.
It validates the season range and can take a SQLite backup first; the generic scrape path does
neither. Use `nfl_estimate_backfill` to size the run before committing to it.

**Closing the quality loop:** coverage and quality rules run automatically after every scrape
job, so findings accumulate without being asked for. Read them with `nfl_get_quality_findings`
and clear them with `nfl_enqueue_repairs` — repairs are idempotent re-scrapes, so retrying is
always safe. Never propose a correction for something a repair job would fix.

## Runbooks

### Capture live odds (2026 season onward)
1. Ensure `OddsPoll:Enabled` is true in API config (default: daily scheduler).
2. Manual run: `nfl_trigger_scrape(type=odds-poll)` or `POST /api/v1/scrape/odds-poll`.
3. Opening lines are captured on first poll; closing lines when the game goes final.
4. Check `nfl_get_game_odds(id)` and coverage `GamesWithOdds` separately from stats coverage.

### Backfill a season
1. `nfl_start_backfill(startSeason=2006, endSeason=2006)` — or use games/stats per week.
2. Poll `nfl_get_job` / `nfl_get_backfill_progress` until complete.
3. `nfl_get_coverage(season=2006)` — verify green weeks.
4. `nfl_get_quality_findings` then `nfl_enqueue_repairs` — clear what the rules caught.
5. `nfl_find_gaps` — address anything still outstanding.

### 20-year Hermes backfill (Phase E)
Runs on the **local agent** — SQLite is the write target; PostgreSQL is the publish target.

1. **Size the run:** `nfl_estimate_backfill(startSeason=2006, endSeason=2025)` — expect ~5,900 API
   calls and ~2.5h of fetch at the default 1.5s delay.
2. **Scrape teams** once (current season enrichment): `nfl_trigger_scrape(type=teams)`
3. **Start backfill** with a backup, reverse-chronological internally:
   `nfl_start_backfill(startSeason=2006, endSeason=2025, backupFirst=true)`
4. **Monitor:** `nfl_get_backfill_progress(jobId)` — pause with `nfl_pause_backfill` if the machine needs to sleep
5. **Resume after reboot:** worker recovers orphaned jobs; call `nfl_resume_backfill` if paused
6. **Verify coverage:** `nfl_get_coverage` + dashboard heat map at `/admin/coverage`
7. **Clear findings:** `nfl_get_quality_findings` → `nfl_enqueue_repairs`, then re-check coverage.
   Expect the first few seasons to surface parse issues the rules then catch for the rest.
8. **Publish:** `nfl_trigger_push` (or `push --resume` on CLI) promotes SQLite → PostgreSQL

### Publish to PostgreSQL
1. Ensure `ConnectionStrings:PostgreSQL` is configured.
2. `nfl_get_push_status` — check for an interrupted session.
3. `nfl_trigger_push(resume=true)` to continue, or `nfl_trigger_push(reset=true)` to start fresh.
4. Push runs in batched stages (default 500 rows); safe to interrupt and resume.

### Game missing box score
1. Confirm game exists: `nfl_get_game(id)`.
2. `nfl_trigger_scrape(type=stats, season, seasonType, week)`.
3. Re-check coverage; if still missing, note ESPN may not have data for that era.

### Failed job
1. `nfl_get_job(jobId)` — read error message.
2. `nfl_retry_job(jobId)` for transient failures.
3. For systematic parse errors, file a correction proposal or escalate.

## Query stats

Use `nfl_query_stats` for aggregations — not free SQL. Example body:
```json
{
  "dataset": "player_game_stats",
  "measure": "passYards",
  "aggregation": "sum",
  "groupBy": ["player", "season"],
  "filters": [{ "field": "season", "op": "=", "value": "2024" }],
  "limit": 25
}
```

## Historical data notes

- Players for past seasons are discovered from box scores, not roster endpoints.
- Team identity is per-season via `TeamSeason` / `Franchise` (relocations: STL→LAR, OAK→LV, etc.).
- Odds, play-by-play, and pre-2002 ESPN coverage are limited — check coverage matrix (`GamesWithOdds` is tracked separately from game/stats coverage).
- Use `nfl_get_game_odds` for historical lines; absence does not mean the game is missing.
