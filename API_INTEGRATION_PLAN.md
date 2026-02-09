# API Integration Plan — Multi-Provider Data Source Architecture

## Goal

Extend the existing HTML-scraping application to support multiple REST/JSON API data sources (ESPN, SportsData.io, MySportsFeeds, NFL.com) while preserving the current Pro Football Reference scraper. The architecture must make it trivial to add new API providers in the future.

---

## Current State

The application is **HTML-only**. All four scraper services inherit from `BaseScraperService`, which exposes a single `FetchPageAsync(url)` method returning an `HtmlDocument`. Every scraper uses XPath to parse HTML tables from Pro Football Reference. There is no JSON deserialization anywhere in the codebase, and no `System.Text.Json` package reference.

**What already works well and will be preserved:**
- The four scraper interfaces (`ITeamScraperService`, `IPlayerScraperService`, `IGameScraperService`, `IStatsScraperService`)
- The repository layer (all `IRepository<T>` + specialized repos with `UpsertAsync`)
- The domain models (`Team`, `Player`, `Game`, `PlayerGameStats`)
- The Polly resilience pipelines (retry, circuit breaker, timeout)
- The `RateLimiterService` singleton
- The CLI command dispatch in `Program.cs`
- The EF Core database layer and migrations

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────┐
│  Program.cs  (CLI dispatch — unchanged interfaces)           │
│  dotnet run -- teams --source espn                           │
├──────────────────────────────────────────────────────────────┤
│  ITeamScraperService / IPlayerScraperService / ...           │
│  (existing interfaces — unchanged)                           │
├────────────┬─────────────┬──────────────┬────────────────────┤
│  Providers │             │              │                    │
│            │             │              │                    │
│  ProFootball  ESPN      SportsData   MySportsFeeds  NFL.com │
│  Reference    API       .io API      API            API     │
│  (HTML)       (JSON)    (JSON)       (JSON)         (JSON)  │
│            │             │              │                    │
├────────────┴─────────────┴──────────────┴────────────────────┤
│  BaseScraperService          BaseApiService                  │
│  FetchPageAsync(url)         FetchJsonAsync<T>(url)          │
│  → HtmlDocument              → T (deserialized)              │
├──────────────────────────────────────────────────────────────┤
│  RateLimiterService  •  Polly Resilience  •  HttpClient      │
├──────────────────────────────────────────────────────────────┤
│  ITeamRepository / IPlayerRepository / ...  (unchanged)      │
├──────────────────────────────────────────────────────────────┤
│  AppDbContext  →  SQLite / PostgreSQL / SQL Server            │
└──────────────────────────────────────────────────────────────┘
```

### Key Design Decisions

1. **The four scraper interfaces remain unchanged.** Every provider implements the same `ITeamScraperService`, `IPlayerScraperService`, etc. This means `Program.cs` and the repository layer need zero changes to support a new provider.

2. **Provider selection is config-driven.** A single `DataProvider` setting in `appsettings.json` controls which implementations get registered in DI. The value maps to a provider name (`ProFootballReference`, `Espn`, `SportsDataIo`, `MySportsFeeds`, `NflCom`).

3. **New base class for API providers.** `BaseApiService` sits alongside `BaseScraperService` and provides `FetchJsonAsync<T>(url)` using `System.Text.Json`. API-based scrapers inherit from `BaseApiService` instead of `BaseScraperService`.

4. **Per-provider configuration.** Each provider can define its own base URL, API key, rate limit delay, and custom headers in a dedicated config section.

5. **Adding a new provider** = create 4 scraper classes (one per entity) in a new folder + add a config section + register in the provider factory. No existing code needs to change.

---

## Target API Providers

### 1. ESPN API (Free, No Key Required)

| Detail | Value |
|---|---|
| **Auth** | None — fully open |
| **Base URL** | `site.api.espn.com/apis/site/v2/sports/football/nfl` |
| **Rate Limit** | Undocumented — use 1000ms delay as safe default |
| **Format** | JSON |
| **Status** | Undocumented but stable and widely used |

**Endpoints:**

| Data | Endpoint | Key Response Fields |
|---|---|---|
| Teams | `/teams` | `sports[0].leagues[0].teams[].team.{id, abbreviation, displayName, location, shortDisplayName}` |
| Roster | `/teams/{espnId}/roster` | `athletes[].{displayName, jersey, position.abbreviation, height, weight, college}` grouped by position categories |
| Scores | `/scoreboard?dates={YYYYMMDD}&week={n}&seasontype=2` | `events[].competitions[].{competitors[].{homeAway, team.abbreviation, score}, date, week}` |
| Stats | `/summary?event={eventId}` | `boxscore.players[].statistics[].athletes[].stats[]` |

**Notes:**
- ESPN uses its own numeric team IDs — a mapping to standard NFL abbreviations is needed.
- Game events have ESPN-specific event IDs required for box score lookups.
- The `seasontype` parameter: 1=preseason, 2=regular, 3=postseason.

---

### 2. SportsData.io (Free Tier — API Key Required)

| Detail | Value |
|---|---|
| **Auth** | Header: `Ocp-Apim-Subscription-Key: {key}` |
| **Base URL** | `api.sportsdata.io/v3/nfl` |
| **Rate Limit** | Free tier ~1,000 calls/month |
| **Format** | JSON |
| **Status** | Officially documented, stable |

**Endpoints:**

| Data | Endpoint | Key Response Fields |
|---|---|---|
| Teams | `/scores/json/Teams` | `[].{TeamID, Key, FullName, City, Conference, Division}` |
| Players | `/scores/json/Players/{team}` | `[].{PlayerID, Name, Team, Position, Jersey, Height, Weight, College}` |
| Games | `/scores/json/ScoresByWeek/{season}/{week}` | `[].{GameKey, Season, Week, HomeTeam, AwayTeam, HomeScore, AwayScore, Date}` |
| Stats | `/stats/json/PlayerGameStatsByWeek/{season}/{week}` | `[].{PlayerID, Name, PassingCompletions, PassingAttempts, PassingYards, PassingTouchdowns, PassingInterceptions, RushingAttempts, RushingYards, RushingTouchdowns, Receptions, ReceivingYards, ReceivingTouchdowns}` |

**Notes:**
- Uses standard NFL abbreviations (`KC`, `ARI`, etc.) — no mapping needed.
- Flat JSON arrays — straightforward deserialization.
- Free tier is generous for development; paid tiers available for production.

---

### 3. MySportsFeeds (Free for Non-Commercial — API Key Required)

| Detail | Value |
|---|---|
| **Auth** | HTTP Basic: username = `{apiKey}`, password = `MYSPORTSFEEDS` |
| **Base URL** | `api.mysportsfeeds.com/v2.1/pull/nfl` |
| **Rate Limit** | Free tier ~250 calls/day |
| **Format** | JSON |
| **Status** | Documented, stable, versioned |

**Endpoints:**

| Data | Endpoint | Key Response Fields |
|---|---|---|
| Teams | `/{season}/teams.json` | `teams[].team.{id, abbreviation, name, city, conference, division}` |
| Players | `/players.json?team={abbr}&season={season}` | `players[].player.{id, firstName, lastName, position, jerseyNumber, height, weight, college, currentTeam.abbreviation}` |
| Games | `/{season}/games.json?week={n}` | `games[].schedule.{id, week, startTime, homeTeam.abbreviation, awayTeam.abbreviation, score.homeScoreTotal, score.awayScoreTotal}` |
| Stats | `/{season}/week/{week}/player_gamelogs.json` | `gamelogs[].{player.id, game.id, stats.passing.*, stats.rushing.*, stats.receiving.*}` |

**Notes:**
- Uses standard NFL abbreviations.
- Requires season context for most endpoints.
- Deeply nested JSON — needs careful DTO design.

---

### 4. NFL.com API (Free, Undocumented)

| Detail | Value |
|---|---|
| **Auth** | None (public endpoints) |
| **Base URL** | `site.api.nfl.com/v1` |
| **Rate Limit** | Unknown — use 1500ms delay as safe default |
| **Format** | JSON |
| **Status** | Undocumented, may change without notice |

**Endpoints:**

| Data | Endpoint | Key Response Fields |
|---|---|---|
| Teams | `/teams` | `teams[].{abbreviation, fullName, nickName, cityStateRegion, conference, division}` |
| Roster | `/teams/{abbr}/roster` | `roster[].{displayName, jerseyNumber, position, height, weight, college}` |
| Scores | `/games?season={year}&seasonType=REG&week={n}` | `games[].{homeTeam.abbreviation, awayTeam.abbreviation, homeTeamScore.pointTotal, awayTeamScore.pointTotal, gameDetailId, week}` |
| Stats | `/games/{gameDetailId}/stats` | Nested player stats by category |

**Notes:**
- Endpoints may change at any time — most fragile provider.
- Marked as a "best effort" provider in code and documentation.
- No API key required but aggressive rate limiting suspected.

---

## Phased Implementation Plan

---

### Phase 1: Core Provider Infrastructure

**Goal:** Build the extensible foundation without breaking existing functionality.

**Files to create:**

| # | File | Purpose |
|---|---|---|
| 1 | `Models/DataProvider.cs` | Enum: `ProFootballReference`, `Espn`, `SportsDataIo`, `MySportsFeeds`, `NflCom` |
| 2 | `Models/ApiProviderSettings.cs` | Config POCO: `BaseUrl`, `ApiKey`, `RequestDelayMs`, `AuthType`, `CustomHeaders` |
| 3 | `Services/Scrapers/BaseApiService.cs` | Abstract base class with `FetchJsonAsync<T>(url)`, shared `HttpClient`, rate limiting, auth header injection |

**Files to modify:**

| # | File | Change |
|---|---|---|
| 4 | `Models/ScraperSettings.cs` | Add `DataProvider` property (default `ProFootballReference`), add `Dictionary<string, ApiProviderSettings> Providers` |
| 5 | `appsettings.json` | Add `DataProvider` key and `Providers` section with per-provider config blocks |
| 6 | `WebScraper.csproj` | No new package needed — `System.Text.Json` is included in .NET 8 runtime |

**Details:**

`ScraperSettings.cs` additions:
```csharp
public string DataProvider { get; set; } = "ProFootballReference";
public Dictionary<string, ApiProviderSettings> Providers { get; set; } = new();
```

`ApiProviderSettings.cs`:
```csharp
public class ApiProviderSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string AuthType { get; set; } = "None";   // None, Header, Basic
    public string? AuthHeaderName { get; set; }       // e.g. "Ocp-Apim-Subscription-Key"
    public int RequestDelayMs { get; set; } = 1000;
    public Dictionary<string, string> CustomHeaders { get; set; } = new();
}
```

`BaseApiService.cs`:
```csharp
public abstract class BaseApiService
{
    protected readonly HttpClient _httpClient;
    protected readonly ILogger _logger;
    protected readonly ApiProviderSettings _providerSettings;
    private readonly RateLimiterService _rateLimiter;

    // FetchJsonAsync<T>(string url) — GET, deserialize, log, handle errors
    // ConfigureAuth() — applies ApiKey/Basic auth per provider config
}
```

**`appsettings.json` additions:**
```json
{
  "ScraperSettings": {
    "DataProvider": "ProFootballReference",
    "RequestDelayMs": 1500,
    "MaxRetries": 3,
    "UserAgent": "NFLScraper/1.0 (educational project)",
    "TimeoutSeconds": 30,
    "Providers": {
      "Espn": {
        "BaseUrl": "https://site.api.espn.com/apis/site/v2/sports/football/nfl",
        "AuthType": "None",
        "RequestDelayMs": 1000
      },
      "SportsDataIo": {
        "BaseUrl": "https://api.sportsdata.io/v3/nfl",
        "ApiKey": "",
        "AuthType": "Header",
        "AuthHeaderName": "Ocp-Apim-Subscription-Key",
        "RequestDelayMs": 1000
      },
      "MySportsFeeds": {
        "BaseUrl": "https://api.mysportsfeeds.com/v2.1/pull/nfl",
        "ApiKey": "",
        "AuthType": "Basic",
        "RequestDelayMs": 1500
      },
      "NflCom": {
        "BaseUrl": "https://site.api.nfl.com/v1",
        "AuthType": "None",
        "RequestDelayMs": 1500
      }
    }
  }
}
```

---

### Phase 2: Provider Factory & DI Wiring

**Goal:** Make `ServiceCollectionExtensions` select the correct scraper implementations based on the `DataProvider` config value.

**Files to create:**

| # | File | Purpose |
|---|---|---|
| 1 | `Services/DataProviderFactory.cs` | Static helper that maps `DataProvider` string → the correct set of `(TInterface, TImplementation)` type pairs for DI registration |

**Files to modify:**

| # | File | Change |
|---|---|---|
| 2 | `Extensions/ServiceCollectionExtensions.cs` | Replace hardcoded `AddScraperHttpClient<ITeamScraperService, TeamScraperService>(...)` calls with provider-driven registration. Add `AddApiScraperHttpClient` method that injects auth headers and provider-specific `BaseUrl` into the `HttpClient` |

**Details:**

The factory pattern:
```csharp
public static class DataProviderFactory
{
    public static void RegisterScrapers(IServiceCollection services,
        ScraperSettings settings)
    {
        switch (settings.DataProvider.ToLowerInvariant())
        {
            case "profootballreference":
                // Register existing HTML scrapers (unchanged)
                AddScraperHttpClient<ITeamScraperService, TeamScraperService>(...);
                ...
                break;
            case "espn":
                AddApiHttpClient<ITeamScraperService, EspnTeamService>(...);
                ...
                break;
            case "sportsdataio":
                AddApiHttpClient<ITeamScraperService, SportsDataTeamService>(...);
                ...
                break;
            // etc.
        }
    }
}
```

`AddApiHttpClient` differs from `AddScraperHttpClient` by:
- Setting `HttpClient.BaseAddress` from the provider's `BaseUrl`
- Adding auth headers (API key header or Basic auth) based on `AuthType`
- Using the provider's `RequestDelayMs` for rate limiting
- Still applying the same Polly resilience pipeline

---

### Phase 3: ESPN API Provider

**Goal:** First API provider — validates the entire architecture end-to-end. ESPN is chosen first because it requires no API key, making it the easiest to develop and test against.

**Files to create:**

| # | File | Purpose |
|---|---|---|
| 1 | `Services/Scrapers/Espn/EspnTeamService.cs` | Implements `ITeamScraperService` via ESPN `/teams` endpoint |
| 2 | `Services/Scrapers/Espn/EspnPlayerService.cs` | Implements `IPlayerScraperService` via ESPN `/teams/{id}/roster` endpoint |
| 3 | `Services/Scrapers/Espn/EspnGameService.cs` | Implements `IGameScraperService` via ESPN `/scoreboard` endpoint |
| 4 | `Services/Scrapers/Espn/EspnStatsService.cs` | Implements `IStatsScraperService` via ESPN `/summary` endpoint |
| 5 | `Services/Scrapers/Espn/EspnDtos.cs` | DTO classes matching ESPN JSON response shapes |
| 6 | `Services/Scrapers/Espn/EspnMappings.cs` | ESPN team ID → NFL abbreviation mapping (ESPN uses numeric IDs internally) |

**Key implementation details:**

- Each service inherits from `BaseApiService`
- Each service injects its entity repository (same as the HTML scrapers do)
- DTOs are deserialized via `FetchJsonAsync<EspnTeamsResponse>(url)`, then mapped to the domain models (`Team`, `Player`, etc.) before calling `repository.UpsertAsync()`
- ESPN uses numeric team IDs — `EspnMappings` provides a bidirectional lookup between ESPN IDs and NFL abbreviations

**ESPN-specific challenge — Stats:**
ESPN's box score endpoint requires an ESPN event ID, not a date+team key. The `EspnGameService` must store or derive these event IDs during game scraping so `EspnStatsService` can fetch box scores. Approach: store the ESPN event ID in a transient lookup (dictionary keyed by `season+week+homeTeamId`) that persists in-memory during a single scrape session.

**Directory structure after Phase 3:**
```
Services/Scrapers/
├── BaseScraperService.cs          (existing — untouched)
├── BaseApiService.cs              (Phase 1)
├── IScraperService.cs             (existing — untouched)
├── TeamScraperService.cs          (existing — untouched)
├── PlayerScraperService.cs        (existing — untouched)
├── GameScraperService.cs          (existing — untouched)
├── StatsScraperService.cs         (existing — untouched)
└── Espn/
    ├── EspnTeamService.cs
    ├── EspnPlayerService.cs
    ├── EspnGameService.cs
    ├── EspnStatsService.cs
    ├── EspnDtos.cs
    └── EspnMappings.cs
```

---

### Phase 4: SportsData.io API Provider

**Goal:** Second provider — validates API-key authentication flow. SportsData.io has the cleanest JSON responses, making it the best candidate to follow ESPN.

**Files to create:**

| # | File | Purpose |
|---|---|---|
| 1 | `Services/Scrapers/SportsDataIo/SportsDataTeamService.cs` | Implements `ITeamScraperService` — calls `/scores/json/Teams` |
| 2 | `Services/Scrapers/SportsDataIo/SportsDataPlayerService.cs` | Implements `IPlayerScraperService` — calls `/scores/json/Players/{team}` |
| 3 | `Services/Scrapers/SportsDataIo/SportsDataGameService.cs` | Implements `IGameScraperService` — calls `/scores/json/ScoresByWeek/{season}/{week}` |
| 4 | `Services/Scrapers/SportsDataIo/SportsDataStatsService.cs` | Implements `IStatsScraperService` — calls `/stats/json/PlayerGameStatsByWeek/{season}/{week}` |
| 5 | `Services/Scrapers/SportsDataIo/SportsDataDtos.cs` | DTO classes for SportsData.io JSON shapes |

**Key implementation details:**

- Auth is a single header (`Ocp-Apim-Subscription-Key`) — injected by `BaseApiService.ConfigureAuth()` at construction time
- SportsData.io uses standard NFL abbreviations (`KC`, `ARI`, etc.) — **no mapping needed**, significantly simpler than ESPN or PFR
- Flat JSON arrays `[]` — DTOs are simple, no deep nesting
- Stats endpoint returns all player stats for an entire week in one call — much more efficient than PFR (one call vs. ~16 box score pages)

**SportsData.io-specific consideration:**
Free tier has a monthly call limit. The `SportsDataStatsService` logs a warning when approaching limits. Consider adding a call counter to `ApiProviderSettings` that tracks usage per session.

---

### Phase 5: MySportsFeeds API Provider

**Goal:** Third provider — validates HTTP Basic authentication and deeply nested JSON responses.

**Files to create:**

| # | File | Purpose |
|---|---|---|
| 1 | `Services/Scrapers/MySportsFeeds/MySportsFeedsTeamService.cs` | Implements `ITeamScraperService` — calls `/{season}/teams.json` |
| 2 | `Services/Scrapers/MySportsFeeds/MySportsFeedsPlayerService.cs` | Implements `IPlayerScraperService` — calls `/players.json?team={abbr}&season={season}` |
| 3 | `Services/Scrapers/MySportsFeeds/MySportsFeedsGameService.cs` | Implements `IGameScraperService` — calls `/{season}/games.json?week={n}` |
| 4 | `Services/Scrapers/MySportsFeeds/MySportsFeedsStatsService.cs` | Implements `IStatsScraperService` — calls `/{season}/week/{week}/player_gamelogs.json` |
| 5 | `Services/Scrapers/MySportsFeeds/MySportsFeedsDtos.cs` | DTO classes for MySportsFeeds JSON shapes |

**Key implementation details:**

- Auth is HTTP Basic: username = API key, password = `"MYSPORTSFEEDS"` — `BaseApiService.ConfigureAuth()` builds the `Authorization: Basic {base64}` header
- Uses standard NFL abbreviations — no mapping needed
- Deeply nested JSON (`games[].schedule.homeTeam.abbreviation`) — DTOs need nested classes but deserialization is straightforward with `System.Text.Json`
- Player names split into `firstName` / `lastName` — concatenate during mapping to `Player.Name`

---

### Phase 6: NFL.com API Provider

**Goal:** Fourth provider — marked as experimental due to undocumented/unstable nature.

**Files to create:**

| # | File | Purpose |
|---|---|---|
| 1 | `Services/Scrapers/NflCom/NflComTeamService.cs` | Implements `ITeamScraperService` |
| 2 | `Services/Scrapers/NflCom/NflComPlayerService.cs` | Implements `IPlayerScraperService` |
| 3 | `Services/Scrapers/NflCom/NflComGameService.cs` | Implements `IGameScraperService` |
| 4 | `Services/Scrapers/NflCom/NflComStatsService.cs` | Implements `IStatsScraperService` |
| 5 | `Services/Scrapers/NflCom/NflComDtos.cs` | DTO classes for NFL.com JSON shapes |

**Key implementation details:**

- No auth required — open endpoints
- Endpoint paths and response shapes may change — each service should log detailed warnings on unexpected JSON structure and fail gracefully (return empty results rather than throw)
- Add `[Experimental]` attribute or XML doc comment on each class to signal instability
- Uses standard NFL abbreviations

---

### Phase 7: CLI Updates & `--source` Flag

**Goal:** Let the user switch providers from the command line as an override to the config default.

**Files to modify:**

| # | File | Change |
|---|---|---|
| 1 | `Program.cs` | Add `--source` flag parsing. If provided, override `ScraperSettings.DataProvider` before building the host. Update `PrintUsage()` with new flag and provider names. |

**CLI after changes:**
```bash
# Uses default provider from appsettings.json
dotnet run -- teams

# Override provider for this run
dotnet run -- teams --source espn
dotnet run -- games --season 2025 --source sportsdataio
dotnet run -- stats --season 2025 --week 1 --source mysportsfeeds

# List available providers
dotnet run -- providers
```

**Implementation approach:**
The `--source` flag must be parsed **before** `Host.CreateDefaultBuilder` is called, because the provider selection determines which DI registrations happen. Approach:
1. Pre-parse `args` for `--source` value
2. If present, override the `DataProvider` value via `ConfigureAppConfiguration` (in-memory config override)
3. The existing `AddWebScraperServices` reads the (possibly overridden) `DataProvider` and registers accordingly

---

### Phase 8: Tests

**Goal:** Test all new code — both unit tests (JSON parsing/mapping) and integration-style tests (DI registration per provider).

**Test files to create:**

| # | File | Tests |
|---|---|---|
| 1 | `tests/WebScraper.Tests/Services/BaseApiServiceTests.cs` | `FetchJsonAsync` deserialization, error handling (null response, malformed JSON, HTTP errors), auth header injection for each `AuthType` |
| 2 | `tests/WebScraper.Tests/Services/DataProviderFactoryTests.cs` | Verify correct implementations registered for each provider string, invalid provider throws |
| 3 | `tests/WebScraper.Tests/Scrapers/Espn/EspnTeamServiceTests.cs` | Parse sample ESPN teams JSON → correct `Team` models, ESPN ID mapping |
| 4 | `tests/WebScraper.Tests/Scrapers/Espn/EspnGameServiceTests.cs` | Parse sample ESPN scoreboard JSON → correct `Game` models, home/away detection |
| 5 | `tests/WebScraper.Tests/Scrapers/Espn/EspnMappingsTests.cs` | All 32 ESPN team IDs map to correct NFL abbreviations |
| 6 | `tests/WebScraper.Tests/Scrapers/SportsDataIo/SportsDataTeamServiceTests.cs` | Parse sample SportsData.io teams JSON → correct models |
| 7 | `tests/WebScraper.Tests/Scrapers/SportsDataIo/SportsDataStatsServiceTests.cs` | Parse sample stats JSON → correct `PlayerGameStats` field mapping |
| 8 | `tests/WebScraper.Tests/Scrapers/MySportsFeeds/MySportsFeedsTeamServiceTests.cs` | Parse nested teams JSON → correct models |
| 9 | `tests/WebScraper.Tests/Scrapers/MySportsFeeds/MySportsFeedsPlayerServiceTests.cs` | First/last name concatenation, nested JSON parsing |
| 10 | `tests/WebScraper.Tests/Scrapers/NflCom/NflComTeamServiceTests.cs` | Parse sample JSON, graceful handling of unexpected structure |
| 11 | `tests/WebScraper.Tests/Configuration/ProviderConfigTests.cs` | `appsettings.json` binds correctly, per-provider settings resolve, missing API key logs warning |

**Testing approach:**
- Use mock `HttpMessageHandler` to return canned JSON responses — no live API calls in tests
- Store sample JSON payloads as embedded resources or string constants in test files
- Reuse the existing `TestDbContextFactory` for repository integration
- Verify DTO → domain model mapping logic in isolation

---

### Phase 9: Documentation & Polish

**Goal:** Update all documentation and finalize.

**Files to modify:**

| # | File | Change |
|---|---|---|
| 1 | `CLAUDE.md` | Add API providers to Tech Stack, update Project Structure with new folders, add provider config docs, update CLI Commands section with `--source` flag, update Test Coverage table |
| 2 | `AGENT_MVP.md` | Add API integration as a completed phase |
| 3 | `appsettings.json` | Final cleanup — ensure all provider configs have sensible defaults, add comments via adjacent README |
| 4 | `README.md` (if exists) | Update with multi-provider usage examples |

---

## File Impact Summary

### New Files (27 total)

```
WebScraper/
├── Models/
│   ├── DataProvider.cs                                    (Phase 1)
│   └── ApiProviderSettings.cs                             (Phase 1)
├── Services/
│   ├── DataProviderFactory.cs                             (Phase 2)
│   └── Scrapers/
│       ├── BaseApiService.cs                              (Phase 1)
│       ├── Espn/
│       │   ├── EspnTeamService.cs                         (Phase 3)
│       │   ├── EspnPlayerService.cs                       (Phase 3)
│       │   ├── EspnGameService.cs                         (Phase 3)
│       │   ├── EspnStatsService.cs                        (Phase 3)
│       │   ├── EspnDtos.cs                                (Phase 3)
│       │   └── EspnMappings.cs                            (Phase 3)
│       ├── SportsDataIo/
│       │   ├── SportsDataTeamService.cs                   (Phase 4)
│       │   ├── SportsDataPlayerService.cs                 (Phase 4)
│       │   ├── SportsDataGameService.cs                   (Phase 4)
│       │   ├── SportsDataStatsService.cs                  (Phase 4)
│       │   └── SportsDataDtos.cs                          (Phase 4)
│       ├── MySportsFeeds/
│       │   ├── MySportsFeedsTeamService.cs                (Phase 5)
│       │   ├── MySportsFeedsPlayerService.cs              (Phase 5)
│       │   ├── MySportsFeedsGameService.cs                (Phase 5)
│       │   ├── MySportsFeedsStatsService.cs               (Phase 5)
│       │   └── MySportsFeedsDtos.cs                       (Phase 5)
│       └── NflCom/
│           ├── NflComTeamService.cs                       (Phase 6)
│           ├── NflComPlayerService.cs                     (Phase 6)
│           ├── NflComGameService.cs                       (Phase 6)
│           ├── NflComStatsService.cs                      (Phase 6)
│           └── NflComDtos.cs                              (Phase 6)
```

### Modified Files (5 total)

```
WebScraper/
├── Models/ScraperSettings.cs                              (Phase 1)
├── Extensions/ServiceCollectionExtensions.cs              (Phase 2)
├── Program.cs                                             (Phase 7)
├── appsettings.json                                       (Phase 1, 7)
└── CLAUDE.md                                              (Phase 9)
```

### Unchanged Files

All existing scraper services, repositories, models, migrations, and the test project remain untouched until Phase 8 (which only adds new test files).

---

## Adding a New Provider (Future)

After this plan is implemented, adding a sixth provider (e.g., Odds API, CBS Sports) requires:

1. **Create a folder:** `Services/Scrapers/NewProvider/`
2. **Create 5 files:**
   - `NewProviderTeamService.cs` (extends `BaseApiService`, implements `ITeamScraperService`)
   - `NewProviderPlayerService.cs` (implements `IPlayerScraperService`)
   - `NewProviderGameService.cs` (implements `IGameScraperService`)
   - `NewProviderStatsService.cs` (implements `IStatsScraperService`)
   - `NewProviderDtos.cs` (JSON response DTOs)
3. **Add config** in `appsettings.json` under `Providers.NewProvider`
4. **Add a case** in `DataProviderFactory.RegisterScrapers()` for `"newprovider"`
5. **Add tests** in `tests/WebScraper.Tests/Scrapers/NewProvider/`

No changes to interfaces, repositories, models, Program.cs, or any other provider's code.

---

## Risk & Mitigation

| Risk | Impact | Mitigation |
|---|---|---|
| ESPN/NFL.com change endpoints | Scraper stops working for that provider | Graceful error handling + fallback to another provider. Log warnings with specific field paths that failed. |
| SportsData.io free tier exhausted | API returns 403 | Log remaining call count. Add `--dry-run` flag for testing URL construction without live calls. |
| JSON schema changes silently | Partial or null data saved | DTO fields are nullable; validation before `UpsertAsync` checks required fields. Log deserialization warnings. |
| Rate limiting by providers | HTTP 429 errors | Polly retry handles 429 with exponential backoff (already configured). Per-provider `RequestDelayMs` tuned conservatively. |
| API key leakage in config | Security exposure | Document use of user secrets (`dotnet user-secrets`) or environment variables for API keys. Never commit keys. Add `appsettings.*.json` to `.gitignore`. |

---

## Implementation Order Rationale

| Phase | Why This Order |
|---|---|
| 1 (Infrastructure) | Must exist before any provider can be built |
| 2 (DI Factory) | Must exist before any provider can be registered |
| 3 (ESPN) | No API key needed — fastest feedback loop for validating the architecture |
| 4 (SportsData.io) | Cleanest JSON + API-key auth validates the auth flow |
| 5 (MySportsFeeds) | Basic auth + nested JSON — exercises remaining auth path |
| 6 (NFL.com) | Most fragile — built last, marked experimental |
| 7 (CLI) | All providers must exist before the `--source` flag is useful |
| 8 (Tests) | Tests are written incrementally per phase but formalized here |
| 9 (Docs) | Final pass after all code is stable |
