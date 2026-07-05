NFL_DB Platform — Execution Plan

Repo: dotnet-AI-football-scraper (.NET 8: WebScraper.Core / WebScraper.Api / WebScraper.Mcp)
Sequence: Search -> Images -> Composites -> Speed -> Media. Ship each workstream independently.

## Workstream 1 — Search & Entity Resolution (days 1-3)

Architecture decision: matching happens in C#, in memory — NOT pg_trgm/FTS5.
One code path works for both SQLite (local) and PostgreSQL (Neon remote).
Index is small (thousands of entries now, ~30k league-wide later); revisit
a DB-native approach only past ~100k entries.

### 1.1 New table: EntityAliases (EF migration)
- Id (int PK)
- EntityType (enum: Player, Team, Venue, Coach)
- EntityId (int)
- Alias (string)               e.g. "Matty Ice"
- NormalizedAlias (string)     lowercased, punctuation/diacritics stripped
- AliasType (enum: Nickname, Misspelling, FormerName, Slang)
- Standard IAuditableEntity + ISoftDeletable columns (9)
- Unique index (EntityType, EntityId, NormalizedAlias)
- Repository: IEntityAliasRepository with UpsertAsync keyed on the unique index

### 1.2 SearchService (WebScraper.Core/Services/Search/)
- Builds an in-memory index: SearchEntry { EntityType, EntityId, NormalizedText, DisplayName, Context }
  - Players: full name (+ context: position, team abbr, years active)
  - Teams: name, city, abbreviation
  - Venues: name, city
  - All EntityAliases rows
- Normalization: lowercase, strip punctuation + diacritics, collapse whitespace
- Scoring tiers: exact = 1.0; prefix = 0.9; trigram Dice coefficient scaled 0-0.85; token-overlap fallback
- Return top N results above 0.4 threshold, each with a confidence score
- Cache: IMemoryCache; invalidate on ScrapeEventRelay JobCompleted event + 10-min TTL
- Unit tests: exact match, typo ("Julio Jonez"), nickname ("Matty Ice"), ambiguous ("Birds"), below-threshold junk

### 1.3 New API endpoints (WebScraper.Api, read scope)
- GET /api/v1/search?q=&types=player,team,venue&limit=5
  -> ranked [{ entityType, id, displayName, context, score }]
- GET /api/v1/players/resolve?name=&team=&season=
  -> single best match or 404 with suggestions[]
- GET /api/v1/games/resolve?season=&team=&opponent=&week=
  -> resolves a game from natural description via team resolution + games query

### 1.4 Alias seed data
- Data/Seeds/aliases.falcons.json + new SeedAliases job type (idempotent upserts)
- Starter aliases: Matty Ice->Matt Ryan; Primetime, Neon Deion->Deion Sanders;
  Dirty Birds, ATL, the Birds->Falcons; Vick->Michael Vick;
  misspellings: Julio Jonez->Julio Jones, Bijon Robinson->Bijan Robinson
- OWNER TASK: review/extend the seed list (~20 min)

### 1.5 MCP + skill
- New tools in WebScraper.Mcp: nfl_search, nfl_resolve_game
- Update skills/nfl-db SKILL.md: Resolution workflow becomes "call nfl_search
  first"; demote the manual list-and-scan pattern to fallback

Definition of done: "matty ice 2016 stats" resolves to Matt Ryan's internal
player ID in one nfl_search call; scorer unit tests green.

## Workstream 2 — Images (days 3-4, overlaps W1)

1. Migration: Players.HeadshotUrl (nullable string), Teams.LogoUrl (string)
2. EspnDtos: add headshot.href to roster athlete DTO; EspnPlayerService stores it during normal scrapes
3. New ImageBackfill job type: players with EspnId and null HeadshotUrl ->
   HEAD https://a.espncdn.com/i/headshots/nfl/players/full/{espnId}.png
   -> store URL on 200, leave null otherwise (frontend renders silhouette)
4. Team logos: static update, https://a.espncdn.com/i/teamlogos/nfl/500/{abbr}.png for all 32
5. Add both URLs to player/team API DTOs (flows through MCP automatically)
6. DECISION (owner): hotlink ESPN CDN for MVP (recommended); backlog item to mirror to Vercel Blob before beta

## Workstream 3 — Composite Endpoints (days 5-8)

One service class per endpoint in Core; thin controllers; DTOs shaped for the
chatbot UI components. Compute live from PlayerGameStats first (correct before
fast) — Workstream 4 swaps internals to aggregates without contract changes.

| Endpoint | Service | Feeds |
|---|---|---|
| GET /players/{id}/card | PlayerCardService | PlayerCard (bio + career totals + HeadshotUrl + achievements) |
| GET /players/compare?a=&b=&categories= | PlayerComparisonService | PlayerComparisonCard (server-computed per-stat advantage flags) |
| GET /seasons/{year}/summary?team= | SeasonSummaryService | SeasonTabs (record, results, top performers) |
| GET /rankings?position=&metric=&limit= | RankingService | PositionRanking |
| GET /players/{id}/career-chart?stat= | CareerChartService | CareerChart (pre-shaped data_points[]) |

MCP tools: nfl_get_player_card, nfl_compare_players, nfl_get_season_summary,
nfl_get_position_ranking, nfl_get_career_chart. Move each from the skill's
"Planned tools" section into the live catalog as it ships.

## Workstream 4 — Speed Layer (days 8-10)

1. New tables: PlayerSeasonStats (player-season grain, all summed stat columns
   + gamesPlayed), PlayerCareerStats (player grain). Rebuilt wholesale, never edited.
2. RebuildAggregates job type, auto-enqueued on stats-scrape JobCompleted
   (same event hook as search-index invalidation — one pattern, two consumers)
3. Swap composite services to read aggregates; keep live-compute path behind a
   config flag as a correctness cross-check
4. ASP.NET output caching: past seasons 24h TTL (immutable), current season 60s,
   /search 5min; invalidation piggybacks on JobCompleted
5. Measure p95 per endpoint from ApiQueryLogs.DurationMs; surface on the
   ApiUsage admin page. Target: p95 < 50ms on every composite endpoint.

## Workstream 5 — MediaVideos + YouTube (days 11-13)

1. MediaVideos table + repository + migration:
   Id, YouTubeVideoId (unique idx, upsert key), Title, ChannelId, ChannelName,
   PublishedAt, ThumbnailUrl, DurationSeconds?, ViewCount?,
   VideoType (enum: Highlight, Interview, Hype, Primetime, News),
   TeamId? (FK), PlayerId? (FK), + standard audit/soft-delete columns
2. Channel config: appsettings list of channel IDs (NFL official, Atlanta
   Falcons) for MVP; promote to a MediaChannels table when multi-team
3. YouTubeMediaService (Core): playlistItems.list per channel uploads playlist
   (1 quota unit/page — never search.list in scheduled paths); map to
   MediaVideo; classify VideoType by title keywords
4. Player tagging: run each title through SearchService scoped to players —
   auto-link PlayerId on confident matches (this is why W1 ships first)
5. MediaPollJob type, two enqueue paths:
   - baseline schedule every 4h
   - post-game burst: when a game's GameStatus flips to final, poll both
     teams' channels every 30 min for 4h
6. GET /api/v1/media/videos?team=&player=&type=&limit= — serves from the table
   only; never calls YouTube in the request path. MCP tool: nfl_get_videos.
   Update skill.
7. OWNER TASK: create Google Cloud project + YouTube Data API v3 key (~10 min)

## Workstream 6 — Rolling backlog
- Skill versioning as tools ship; optional GET /api/v1/skill distribution endpoint
- Madden ratings one-time dataset import (new MaddenRatings table)
- PFR scrape extensions: coaches, draft picks, awards/Pro Bowl/HOF
- Image mirroring to Vercel Blob
- pg_trgm migration if search index exceeds ~100k entries

## Dependency map
W1 Search -> W3 Composites -> W4 Speed
W1 Search -> W5 Media (player tagging)
W2 Images -> W3 (player card includes HeadshotUrl)

## Owner decisions for day 1
1. Hotlink vs mirror images (recommended: hotlink now)
2. In-memory search vs pg_trgm (recommended: in-memory)
3. Review alias seed list once drafted
