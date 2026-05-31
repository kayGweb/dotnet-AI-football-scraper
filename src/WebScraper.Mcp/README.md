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
| `nfl_list_venues` | `GET /api/v1/venues` | Venues (paged, filters: state, indoor/outdoor) |
| `nfl_get_venue` | `GET /api/v1/venues/{id}` | Single venue |
| `nfl_get_status` | `GET /api/v1/status` | DB row counts + freshness |

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
