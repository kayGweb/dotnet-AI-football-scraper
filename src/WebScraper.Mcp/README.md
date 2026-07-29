# WebScraper.Mcp

MCP (Model Context Protocol) server that exposes the WebScraper.Api read endpoints
as tools callable by Claude Code / Claude Desktop / any MCP client.

## What's exposed

| Tool | Endpoint | Purpose |
|------|----------|---------|
| `nfl_list_teams` | `GET /api/v1/teams` | List teams (paged, optional conference filter) |
| `nfl_get_team` | `GET /api/v1/teams/{id}` | Team by primary key |
| `nfl_get_team_by_abbreviation` | `GET /api/v1/teams/by-abbreviation/{abbr}` | Team by NFL abbr (e.g. KC) |
| `nfl_list_players` | `GET /api/v1/players` | Players (paged, filters: team, position) |
| `nfl_get_player` | `GET /api/v1/players/{id}` | Single player |
| `nfl_get_player_stats` | `GET /api/v1/players/{id}/stats` | Player game stats (optional season/week) |
| `nfl_list_games` | `GET /api/v1/games` | Games (paged, filters: season/week/team) |
| `nfl_get_game` | `GET /api/v1/games/{id}` | Single game with venue + quarter scores |
| `nfl_get_game_team_stats` | `GET /api/v1/games/{id}/team-stats` | Team aggregates for a game |
| `nfl_get_game_player_stats` | `GET /api/v1/games/{id}/player-stats` | All player stat lines for a game |
| `nfl_get_game_injuries` | `GET /api/v1/games/{id}/injuries` | Injury reports for a game |
| `nfl_get_game_drives` | `GET /api/v1/games/{id}/drives` | Drive chart |
| `nfl_get_game_scoring_plays` | `GET /api/v1/games/{id}/scoring-plays` | Scoring plays |
| `nfl_get_game_weather` | `GET /api/v1/games/{id}/weather` | Game-day weather |
| `nfl_get_game_officials` | `GET /api/v1/games/{id}/officials` | Referee crew |
| `nfl_get_game_odds` | `GET /api/v1/games/{id}/odds` | Betting odds snapshots |
| `nfl_list_venues` | `GET /api/v1/venues` | Venues (paged, filters: state, indoor/outdoor) |
| `nfl_get_venue` | `GET /api/v1/venues/{id}` | Single venue |
| `nfl_get_status` | `GET /api/v1/status` | DB row counts + freshness |

### Operate tools (`operate` scope)

| Tool | Endpoint | Purpose |
|------|----------|---------|
| `nfl_trigger_scrape` | `POST /api/v1/scrape/{type}` | Start scrape/backfill job |
| `nfl_get_job` | `GET /api/v1/jobs/{id}` | Job status + errors |
| `nfl_list_jobs` | `GET /api/v1/jobs` | List jobs (optional status filter) |
| `nfl_get_coverage` | `GET /api/v1/coverage` | Expected vs actual per week |
| `nfl_find_gaps` | `GET /api/v1/gaps` | Ranked missing/suspect data |
| `nfl_retry_job` | `POST /api/v1/jobs/{id}/retry` | Re-queue a job |
| `nfl_get_backfill_progress` | `GET /api/v1/backfill/{id}/progress` | Backfill child counts + ETA |
| `nfl_pause_backfill` | `POST /api/v1/backfill/{id}/pause` | Pause a running backfill |
| `nfl_resume_backfill` | `POST /api/v1/backfill/{id}/resume` | Resume a paused backfill |

### Publish tools (`admin` scope)

| Tool | Endpoint | Purpose |
|------|----------|---------|
| `nfl_get_push_status` | `GET /api/v1/push/status` | Latest push session checkpoint |
| `nfl_trigger_push` | `POST /api/v1/push?resume=&reset=` | Batched SQLite → PostgreSQL push |
| `nfl_backup_database` | `POST /api/v1/backup` | Timestamped SQLite backup |

### Introspect tools (`read` scope)

| Tool | Endpoint | Purpose |
|------|----------|---------|
| `nfl_describe_schema` | `GET /api/v1/schema` | Entity catalog + columns |
| `nfl_get_data_dictionary` | `GET /api/v1/schema/dictionary` | Stat meanings + rules |
| `nfl_query_stats` | `POST /api/v1/query/stats` | Parameterized aggregation |

### Propose tools (`admin` scope)

| Tool | Endpoint | Purpose |
|------|----------|---------|
| `nfl_propose_correction` | `POST /api/v1/corrections` | Propose field fix for approval |
| `nfl_list_corrections` | `GET /api/v1/corrections` | List correction proposals |

See `skills/nfl-db/SKILL.md` for agent behavior rules.

## Configuration

Two environment variables drive the server:

| Env var | Required | Default | Purpose |
|---------|----------|---------|---------|
| `NFL_API_URL` | recommended | `http://localhost:5080` | Base URL of the WebScraper.Api |
| `NFL_API_KEY` | yes | _empty_ | API key sent via `X-Api-Key` header |

You can also set `Mcp:TimeoutSeconds` (default 30) via env or appsettings.

## Wiring it into Claude Code

In your Claude Code MCP config:

```json
{
  "mcpServers": {
    "nfl": {
      "command": "dotnet",
      "args": ["run", "--project", "src/WebScraper.Mcp", "--no-build"],
      "env": {
        "NFL_API_URL": "http://localhost:5080",
        "NFL_API_KEY": "sk_local_..."
      }
    }
  }
}
```

Run `dotnet build src/WebScraper.Mcp` once first so `--no-build` works.

## Wiring it into Claude Desktop

`~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) or
`%APPDATA%\Claude\claude_desktop_config.json` (Windows):

```json
{
  "mcpServers": {
    "nfl": {
      "command": "dotnet",
      "args": [
        "/absolute/path/to/dotnet-AI-football-scraper/src/WebScraper.Mcp/bin/Release/net8.0/WebScraper.Mcp.dll"
      ],
      "env": {
        "NFL_API_URL": "https://your-nfl-api.example.com",
        "NFL_API_KEY": "sk_live_..."
      }
    }
  }
}
```

Publish first: `dotnet publish -c Release src/WebScraper.Mcp`.

## Notes

- **stdout is reserved for the MCP protocol** — all server logs go to stderr.
  If you see "Unexpected token" errors in the client, something printed to stdout.
- The server returns the raw API JSON body. Errors (401, 404, network failure)
  are wrapped in a small `{"error":true,"status":...,"reason":...}` envelope so
  Claude gets actionable feedback rather than a protocol-level failure.
- API key value is the plaintext key; the API hashes it on the server side.
