# SportsData.io SDK Integration Plan

## SDK Assessment

### What is the SDK?
The **FantasyData.Api.Client** NuGet package (v1.4.0, released Oct 2025) is the official C# client library by SportsDataIO (a Sportradar subsidiary). It wraps the entire SportsDataIO REST API into strongly-typed C# methods and model classes — no manual JSON parsing needed.

### Should we use it?

| Factor | Rating | Details |
|--------|--------|---------|
| **Data Richness** | Excellent | 98 NFL model classes vs our current 4. Covers passing, rushing, receiving, defense, kicking, returns, fumbles, snap counts, fantasy points, DFS salaries, injuries, odds, play-by-play, projections, and more. |
| **Maintenance** | Good | v1.4.0 released Oct 2025 with "a boatload of new endpoints." MIT licensed. Official from SportsDataIO. |
| **API Surface** | Excellent | 11 NFL client classes: Stats, Scores, Projections, Odds, PlayByPlay, AdvancedMetrics, Headshots, Baker Predictive, and 3 news clients. ~60+ methods covering every NFL data need. |
| **Integration Fit** | Good | Uses same `Ocp-Apim-Subscription-Key` auth we already support. Same SportsData.io API we already have a raw provider for. |
| **Limitation** | Minor | Targets .NET Framework 4.6.1, not .NET 8. BUT it has zero dependencies, so it will work fine via compatibility shim. We should verify this during Phase 1. |
| **Limitation** | Minor | 27K total downloads — niche package. But it's the official first-party SDK. |

**Verdict: YES — use it.** The SDK eliminates our hand-rolled DTOs and JSON parsing for SportsData.io, gives us access to 10x richer data (defense, kicking, fantasy, injuries, odds, projections), and the strongly-typed clients reduce boilerplate significantly. The main question is whether to extend the existing database or create a new one.

### Recommendation: Extend Existing Database + New Tables

Rather than creating a whole new database, we should:
1. **Keep the existing 4 tables** unchanged (backward compatible with all 5 current providers)
2. **Add new tables** for the richer data only available through the SDK (fantasy projections, injuries, odds, standings, advanced stats)
3. **Enrich existing models** with optional columns that the SDK can populate (defensive stats, kicking stats, snap counts on PlayerGameStats; stadium/weather on Game; birth date/draft info on Player)
4. **Create a new `Sportradar` provider** that uses the SDK client library instead of raw HTTP/JSON

This gives us the best of both worlds — existing providers still work, and the SDK provider unlocks the full dataset.

---

## Architecture Overview

```
Program.cs (CLI dispatch — unchanged)
    ↓
ITeamScraperService / IPlayerScraperService / IGameScraperService / IStatsScraperService
    +
IFantasyService / IInjuryService / IOddsService / IStandingsService (NEW interfaces)
    ↓
SportradarTeamService / SportradarPlayerService / etc. (NEW — uses SDK clients)
    ↓
NFLv3StatsClient / NFLv3ScoresClient / NFLv3ProjectionsClient (SDK)
    ↓
Repository Layer (extended with new repos)
    ↓
AppDbContext → SQLite / PostgreSQL / SQL Server
```

---

## Implementation Phases

### Phase 1: SDK Integration & Validation (Foundation)
**Goal:** Add the NuGet package, verify .NET 8 compatibility, create SDK client factory.

**Files to create/modify:**
- `WebScraper/WebScraper.csproj` — Add `FantasyData.Api.Client` v1.4.0 NuGet reference
- `WebScraper/Services/SportradarClientFactory.cs` — NEW: Factory that creates SDK client instances (`NFLv3StatsClient`, `NFLv3ScoresClient`, `NFLv3ProjectionsClient`) using the API key from config
- `WebScraper/appsettings.json` — Add `Sportradar` provider config under `Providers`

**Validation steps:**
- `dotnet restore` + `dotnet build` to confirm .NET 8 compatibility
- Write a quick smoke test that instantiates an SDK client

**Config addition:**
```json
"Sportradar": {
  "BaseUrl": "https://api.sportsdata.io/v3/nfl",
  "ApiKey": "",
  "AuthType": "Header",
  "AuthHeaderName": "Ocp-Apim-Subscription-Key",
  "RequestDelayMs": 1200
}
```

---

### Phase 2: Enrich Existing Models (Schema Extension)
**Goal:** Add optional columns to existing entities for the richer SDK data. All new fields are nullable so existing providers continue to work unchanged.

**Player model additions:**
```csharp
// New fields (all nullable — only populated by SDK provider)
public DateTime? BirthDate { get; set; }
public int? Age { get; set; }
public int? Experience { get; set; }
public string? Status { get; set; }           // Active, Injured Reserve, etc.
public string? PhotoUrl { get; set; }
public string? FantasyPosition { get; set; }
public int? CollegeDraftRound { get; set; }
public int? CollegeDraftPick { get; set; }
public int? CollegeDraftYear { get; set; }
public string? CollegeDraftTeam { get; set; }
public int? SportradarPlayerId { get; set; }  // External ID for cross-referencing
```

**Game model additions:**
```csharp
// New fields (all nullable)
public string? Stadium { get; set; }
public string? PlayingSurface { get; set; }
public int? Temperature { get; set; }
public int? Humidity { get; set; }
public int? WindSpeed { get; set; }
public decimal? PointSpread { get; set; }
public decimal? OverUnder { get; set; }
public bool? IsGameOver { get; set; }
public int? HomeScoreQ1 { get; set; }
public int? HomeScoreQ2 { get; set; }
public int? HomeScoreQ3 { get; set; }
public int? HomeScoreQ4 { get; set; }
public int? AwayScoreQ1 { get; set; }
public int? AwayScoreQ2 { get; set; }
public int? AwayScoreQ3 { get; set; }
public int? AwayScoreQ4 { get; set; }
public string? GameKey { get; set; }          // SportsData.io GameKey for cross-ref
```

**PlayerGameStats model additions:**
```csharp
// Defensive stats
public int DefensiveSoloTackles { get; set; }
public int DefensiveAssistedTackles { get; set; }
public int DefensiveSacks { get; set; }
public int DefensiveInterceptions { get; set; }
public int DefensiveForcedFumbles { get; set; }
public int DefensiveFumblesRecovered { get; set; }
public int DefensivePassesDefended { get; set; }
public decimal? DefensiveTacklesForLoss { get; set; }
public decimal? DefensiveQuarterbackHits { get; set; }

// Kicking stats
public int FieldGoalsMade { get; set; }
public int FieldGoalsAttempted { get; set; }
public int ExtraPointsMade { get; set; }
public int ExtraPointsAttempted { get; set; }

// Return stats
public int PuntReturnYards { get; set; }
public int PuntReturnTouchdowns { get; set; }
public int KickReturnYards { get; set; }
public int KickReturnTouchdowns { get; set; }

// Fumbles
public int FumblesLost { get; set; }

// Snap counts
public int? OffensiveSnapsPlayed { get; set; }
public int? DefensiveSnapsPlayed { get; set; }
public int? SpecialTeamsSnapsPlayed { get; set; }

// Fantasy points
public decimal? FantasyPoints { get; set; }
public decimal? FantasyPointsPPR { get; set; }
public decimal? FantasyPointsDraftKings { get; set; }
public decimal? FantasyPointsFanDuel { get; set; }
```

**Team model additions:**
```csharp
public int? SportradarTeamId { get; set; }    // External ID
public int? Wins { get; set; }
public int? Losses { get; set; }
public int? Ties { get; set; }
public string? HeadCoach { get; set; }
```

**Migration:**
- `WebScraper/Migrations/` — New migration: `AddSportradarEnrichedFields`

---

### Phase 3: New Domain Models & Tables (Fantasy, Injuries, Standings)
**Goal:** Create new entities for data categories that don't fit in existing tables.

**New Models:**

**`Models/FantasyProjection.cs`** — Player fantasy projections (weekly/seasonal)
```csharp
public class FantasyProjection
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public int Season { get; set; }
    public int? Week { get; set; }              // null = season projection
    public string ProjectionType { get; set; }  // "Weekly" or "Season"

    // Projected stats
    public decimal? ProjectedPassYards { get; set; }
    public decimal? ProjectedPassTouchdowns { get; set; }
    public decimal? ProjectedRushYards { get; set; }
    public decimal? ProjectedRushTouchdowns { get; set; }
    public decimal? ProjectedReceivingYards { get; set; }
    public decimal? ProjectedReceivingTouchdowns { get; set; }
    public decimal? ProjectedReceptions { get; set; }

    // Fantasy points
    public decimal? FantasyPoints { get; set; }
    public decimal? FantasyPointsPPR { get; set; }
    public decimal? FantasyPointsDraftKings { get; set; }
    public decimal? FantasyPointsFanDuel { get; set; }

    // DFS salaries
    public int? DraftKingsSalary { get; set; }
    public int? FanDuelSalary { get; set; }
    public int? YahooSalary { get; set; }

    // ADP
    public decimal? AverageDraftPosition { get; set; }

    public Player Player { get; set; } = null!;
}
```

**`Models/Injury.cs`** — Player injury reports
```csharp
public class Injury
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public int Season { get; set; }
    public int Week { get; set; }
    public string? Status { get; set; }         // Out, Doubtful, Questionable, Probable
    public string? BodyPart { get; set; }
    public string? Notes { get; set; }
    public string? Practice { get; set; }       // Full, Limited, DNP
    public string? PracticeDescription { get; set; }
    public bool DeclaredInactive { get; set; }
    public DateTime? InjuryStartDate { get; set; }
    public DateTime Updated { get; set; }

    public Player Player { get; set; } = null!;
}
```

**`Models/Standing.cs`** — Team standings
```csharp
public class Standing
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public int Season { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Ties { get; set; }
    public decimal WinPercentage { get; set; }
    public int PointsFor { get; set; }
    public int PointsAgainst { get; set; }
    public int DivisionWins { get; set; }
    public int DivisionLosses { get; set; }
    public int ConferenceWins { get; set; }
    public int ConferenceLosses { get; set; }
    public int? DivisionRank { get; set; }
    public int? ConferenceRank { get; set; }

    public Team Team { get; set; } = null!;
}
```

**New Repositories:**
- `Data/Repositories/IFantasyProjectionRepository.cs` + `FantasyProjectionRepository.cs`
- `Data/Repositories/IInjuryRepository.cs` + `InjuryRepository.cs`
- `Data/Repositories/IStandingRepository.cs` + `StandingRepository.cs`

**Migration:**
- New migration: `AddFantasyInjuryStandingTables`

---

### Phase 4: Sportradar Provider Services (Core Implementation)
**Goal:** Create the new provider that uses the SDK client library instead of raw HTTP.

**New Files:**
```
Services/Scrapers/Sportradar/
├── SportradarTeamService.cs       # Uses NFLv3ScoresClient.GetTeams()
├── SportradarPlayerService.cs     # Uses NFLv3ScoresClient.GetPlayersByTeam()
├── SportradarGameService.cs       # Uses NFLv3ScoresClient.GetGamesByWeek()
├── SportradarStatsService.cs      # Uses NFLv3StatsClient.GetPlayerGameStatsByWeek()
├── SportradarFantasyService.cs    # Uses NFLv3ProjectionsClient (NEW interface)
├── SportradarInjuryService.cs     # Uses NFLv3StatsClient.GetInjuriesAll() (NEW interface)
└── SportradarStandingsService.cs  # Uses NFLv3StatsClient.GetStandings() (NEW interface)
```

**Key design decisions:**
- These services do NOT extend `BaseApiService` — the SDK handles HTTP, auth, and deserialization internally
- They DO use `RateLimiterService` for rate limiting (wrap SDK calls)
- They return `ScrapeResult` like all other providers
- They map SDK model types (e.g., `FantasyData.Api.Client.Model.NFLv3.PlayerGame`) to our domain models

**New Interfaces:**
```csharp
// Services/Scrapers/IFantasyService.cs
public interface IFantasyService
{
    Task<ScrapeResult> ScrapeWeeklyProjectionsAsync(int season, int week);
    Task<ScrapeResult> ScrapeSeasonProjectionsAsync(int season);
}

// Services/Scrapers/IInjuryService.cs
public interface IInjuryService
{
    Task<ScrapeResult> ScrapeInjuriesAsync(int season, int week);
}

// Services/Scrapers/IStandingsService.cs
public interface IStandingsService
{
    Task<ScrapeResult> ScrapeStandingsAsync(int season);
}
```

---

### Phase 5: DI Wiring & Provider Registration
**Goal:** Register the Sportradar provider in the factory and DI container.

**Files to modify:**
- `Models/DataProvider.cs` — Add `Sportradar` to the enum
- `Services/DataProviderFactory.cs` — Add `"Sportradar"` case that registers:
  - SDK client instances via `SportradarClientFactory`
  - All 4 existing scraper interfaces (Team, Player, Game, Stats)
  - 3 new interfaces (Fantasy, Injury, Standings)
- `Extensions/ServiceCollectionExtensions.cs` — Register new repositories, conditionally register new interfaces
- `appsettings.json` — Add `Sportradar` provider section

**Important:** The new interfaces (IFantasyService, IInjuryService, IStandingsService) should be registered as no-ops for providers that don't support them, so the rest of the app doesn't break.

---

### Phase 6: CLI & Interactive Mode Extensions
**Goal:** Expose the new data through CLI commands and interactive menus.

**New CLI commands:**
```bash
dotnet run -- projections --season 2025 --week 1     # Scrape fantasy projections
dotnet run -- injuries --season 2025 --week 1        # Scrape injury reports
dotnet run -- standings --season 2025                 # Scrape standings
dotnet run -- list projections --season 2025 --week 1  # View projections
dotnet run -- list injuries --season 2025 --week 1   # View injuries
dotnet run -- list standings --season 2025            # View standings
```

**Interactive mode additions:**
- Scrape submenu: Add options 7-9 for projections, injuries, standings
- View submenu: Add options for viewing projections, injuries, standings
- `ConsoleDisplayService` — Add `PrintProjectionsTable()`, `PrintInjuriesTable()`, `PrintStandingsTable()`

**Files to modify:**
- `Program.cs` — Add CLI dispatch for new commands, update interactive menus
- `Services/ConsoleDisplayService.cs` — Add display methods for new data types

---

### Phase 7: Tests
**Goal:** Comprehensive test coverage for all new code.

**New test files:**
```
tests/WebScraper.Tests/
├── Scrapers/Sportradar/
│   ├── SportradarTeamServiceTests.cs
│   ├── SportradarPlayerServiceTests.cs
│   ├── SportradarGameServiceTests.cs
│   ├── SportradarStatsServiceTests.cs
│   ├── SportradarFantasyServiceTests.cs
│   └── SportradarInjuryServiceTests.cs
├── Repositories/
│   ├── FantasyProjectionRepositoryTests.cs
│   ├── InjuryRepositoryTests.cs
│   └── StandingRepositoryTests.cs
├── Models/
│   └── NewModelTests.cs                      # FantasyProjection, Injury, Standing defaults
└── Services/
    └── SportradarClientFactoryTests.cs
```

**Test strategy:**
- Mock the SDK clients (they're class-based, so use wrapper interfaces or virtual methods)
- Test mapping from SDK models → domain models
- Test repository CRUD + upsert for new entities
- Test DI registration for Sportradar provider
- Update existing DataProviderFactoryTests for new provider

---

### Phase 8: Documentation & Polish
**Goal:** Update all docs, verify everything works end-to-end.

- Update `CLAUDE.md` with new provider, models, CLI commands, test coverage
- Update `appsettings.json` comments
- Run full test suite: `dotnet test`
- Run `dotnet build` to confirm clean compilation
- Verify migration applies cleanly on fresh SQLite database

---

## Data Richness Comparison

| Data Category | Current (4 tables, ~35 fields) | After SDK Integration (~8 tables, ~150+ fields) |
|---------------|-------------------------------|------------------------------------------------|
| **Teams** | Name, City, Conference, Division | + Head Coach, Wins/Losses, External IDs |
| **Players** | Name, Position, Jersey, Height, Weight, College | + Birth Date, Age, Experience, Status, Photo, Draft Info, Fantasy Position |
| **Games** | Season, Week, Date, Scores | + Stadium, Weather, Point Spread, Over/Under, Quarter Scores, Game Status |
| **Offensive Stats** | Pass/Rush/Rec yards & TDs (11 fields) | Same 11 + completion %, rating, long plays, targets |
| **Defensive Stats** | None | Tackles, Sacks, INTs, FF, FR, PD, TFL, QB Hits |
| **Kicking Stats** | None | FG made/attempted, XP made/attempted |
| **Return Stats** | None | Punt/Kick return yards & TDs |
| **Snap Counts** | None | Offensive, Defensive, Special Teams snaps |
| **Fantasy Points** | None | Standard, PPR, DraftKings, FanDuel scoring |
| **Fantasy Projections** | None | Weekly & season projections, DFS salaries, ADP |
| **Injuries** | None | Status, body part, practice participation |
| **Standings** | None | W/L/T, division/conference rank, points for/against |

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| SDK targets .NET 4.6.1, we use .NET 8 | SDK has zero dependencies; .NET 8 supports .NET Standard compatibility. Verify in Phase 1 before proceeding. |
| API key required | Already supported via `Ocp-Apim-Subscription-Key` header in config. User must have SportsData.io subscription. |
| SDK client classes may not be mockable | Create thin wrapper interfaces around SDK clients for testability. |
| Large migration could break existing data | All new fields are nullable; migration is additive only. Existing data untouched. |
| Rate limiting not built into SDK | Wrap all SDK calls with our existing `RateLimiterService`. |

## Estimated Scope
- **New files:** ~25 (services, models, repositories, interfaces, tests)
- **Modified files:** ~12 (existing models, DbContext, factory, Program.cs, display service, csproj, appsettings)
- **New migration:** 2 (enriched fields + new tables)
- **New tests:** ~40-50 test cases
