# Scraper User Interface Implementation Plan

## Goal
Transform the scraper from a silent fire-and-forget CLI into a responsive, interactive application with clear user feedback, data display capabilities, and a menu-driven interactive mode.

---

## Phase 1: ScrapeResult Model + Interface Changes

**Why first:** Everything else depends on scraper methods returning structured results.

### 1a. Create `Models/ScrapeResult.cs`
New model to represent the outcome of any scrape operation:
```csharp
public class ScrapeResult
{
    public bool Success { get; set; }
    public int RecordsProcessed { get; set; }
    public int RecordsFailed { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
}
```

### 1b. Update `IScraperService.cs` — change all 4 interfaces
Change return types from `Task` to `Task<ScrapeResult>`:
- `ITeamScraperService.ScrapeTeamsAsync()` → `Task<ScrapeResult>`
- `ITeamScraperService.ScrapeTeamAsync(string)` → `Task<ScrapeResult>`
- `IPlayerScraperService.ScrapePlayersAsync(int)` → `Task<ScrapeResult>`
- `IPlayerScraperService.ScrapeAllPlayersAsync()` → `Task<ScrapeResult>`
- `IGameScraperService.ScrapeGamesAsync(int)` → `Task<ScrapeResult>`
- `IGameScraperService.ScrapeGamesAsync(int, int)` → `Task<ScrapeResult>`
- `IStatsScraperService.ScrapePlayerStatsAsync(int, int)` → `Task<ScrapeResult>`

### 1c. Update all 20 scraper implementations
Each implementation changes from logging + returning void to building and returning a `ScrapeResult`. The existing logging stays, but now the count/error data also flows back to the caller.

**Files to update (20 total):**
- PFR: `TeamScraperService.cs`, `PlayerScraperService.cs`, `GameScraperService.cs`, `StatsScraperService.cs`
- ESPN: `EspnTeamService.cs`, `EspnPlayerService.cs`, `EspnGameService.cs`, `EspnStatsService.cs`
- SportsData.io: `SportsDataTeamService.cs`, `SportsDataPlayerService.cs`, `SportsDataGameService.cs`, `SportsDataStatsService.cs`
- MySportsFeeds: `MySportsFeedsTeamService.cs`, `MySportsFeedsPlayerService.cs`, `MySportsFeedsGameService.cs`, `MySportsFeedsStatsService.cs`
- NFL.com: `NflComTeamService.cs`, `NflComPlayerService.cs`, `NflComGameService.cs`, `NflComStatsService.cs`

**Pattern for each implementation:**
- Where `return;` was used after a failure → `return new ScrapeResult { Success = false, Message = "..." };`
- Where a count was logged → `return new ScrapeResult { Success = true, RecordsProcessed = count, Message = "..." };`
- Internal logging stays unchanged (Serilog for diagnostics, ScrapeResult for user-facing)

---

## Phase 2: Console Output Service + Startup Banner

**Why second:** Gives us the output layer before we start using it in Program.cs.

### 2a. Create `Services/ConsoleDisplayService.cs`
A thin helper for user-facing console output, separate from Serilog diagnostic logging:
```csharp
public class ConsoleDisplayService
{
    public void PrintBanner(string provider, string database, string connectionInfo);
    public void PrintScrapeResult(string operation, ScrapeResult result);
    public void PrintProgress(string operation, int current, int total);
    public void PrintTeamsTable(IEnumerable<Team> teams);
    public void PrintGamesTable(IEnumerable<Game> games);
    public void PrintPlayersTable(IEnumerable<Player> players);
    public void PrintStatsTable(IEnumerable<PlayerGameStats> stats);
    public void PrintError(string message);
    public void PrintSuccess(string message);
    public void PrintInteractiveMenu();
}
```

Key design decisions:
- Uses `Console.WriteLine` directly (not Serilog) for clean, structured output
- Formats data in aligned, readable tables
- Color-coded status using `Console.ForegroundColor` (green for success, red for errors, yellow for warnings)
- Registered as singleton in DI

### 2b. Early `--source` validation in Program.cs
Before building the DI container, validate the `--source` flag against the known provider names. Print a clean error listing valid providers if invalid, instead of letting it blow up in `DataProviderFactory`.

### 2c. Startup banner
After host build, before command dispatch, print:
```
NFL Web Scraper v1.0
Source: ESPN API | Database: SQLite (data/nfl_data.db)
────────────────────────────────────────────────────
```

---

## Phase 3: Program.cs Rewrite — Result Handling + Exit Codes

**Why third:** Now that interfaces return results and we have a display service, wire it all together.

### 3a. Update `RunCommandAsync` to return `int`
Change signature to `static async Task<int> RunCommandAsync(...)`.
Use the `ScrapeResult` from each scraper to:
- Print a clean summary via `ConsoleDisplayService.PrintScrapeResult()`
- Return exit code 0 for success, 1 for failure (no records or errors)

### 3b. Update `RunAllAsync` to use results
Currently runs teams → players → games silently. Update to:
- Print progress after each step
- Stop early if a critical step fails (e.g., teams must succeed before players)
- Include stats in the pipeline (currently missing)
- Return aggregated result

### 3c. Add dependency checking
Before running `stats`, check if games exist for that season/week. Before running `players`, check if teams exist. Print actionable error messages:
```
No games found for 2025 week 1. Run 'scrape games --season 2025 --week 1' first.
```

---

## Phase 4: Data Display Commands

**Why fourth:** Builds on the ConsoleDisplayService from Phase 2.

### 4a. Add `list` commands to Program.cs
New command group for viewing data already in the database:
- `list teams` — formatted table of all teams (Abbr, Name, City, Conference, Division)
- `list teams --conference AFC` — filter by conference
- `list players --team KC` — roster for a team
- `list games --season 2025` — all games in a season
- `list games --season 2025 --week 1` — games for a specific week with scores
- `list stats --season 2025 --week 1` — player stats for a week
- `list stats --player "Patrick Mahomes" --season 2025` — individual player stats

### 4b. Add `status` command
Shows what data is currently in the database:
```
Database Status:
  Teams:   32
  Players: 1,696
  Games:   272 (Season 2025)
  Stats:   4,352 stat lines
```

Uses repository `GetAllAsync()` counts and existing query methods.

### 4c. Update `PrintUsage()` help text
Add the new `list` and `status` commands to the help output.

---

## Phase 5: Interactive Mode

**Why fifth:** The capstone feature, depends on everything above being in place.

### 5a. Add interactive mode to Program.cs
When run with no arguments (or `dotnet run -- interactive`), launch a menu-driven loop:

```
NFL Web Scraper v1.0
Source: ESPN API | Database: SQLite (data/nfl_data.db)
────────────────────────────────────────────────────

Main Menu:
  1. Scrape data
  2. View data
  3. Database status
  4. Change source
  5. Exit

>
```

### 5b. Scrape submenu
```
Scrape Menu:
  1. Teams (all 32)
  2. Single team
  3. Players (all rosters)
  4. Games (full season)
  5. Games (single week)
  6. Player stats (single week)
  7. Full pipeline (teams + players + games + stats)
  8. Back to main menu

>
```
- For options requiring input (season, week, team abbr), prompt inline
- Print ScrapeResult summary after each operation
- For multi-step operations, show progress (e.g., "Scraping week 3/18...")

### 5c. View data submenu
```
View Menu:
  1. Teams
  2. Players (by team)
  3. Games (by season/week)
  4. Player stats
  5. Back to main menu

>
```
- Prompts for filters inline (team, season, week, player name)
- Displays data using ConsoleDisplayService table formatters

### 5d. Change source option
```
Current source: ESPN API

Available sources:
  1. Pro Football Reference (HTML scraping)
  2. ESPN (JSON API)
  3. SportsData.io (requires API key)
  4. MySportsFeeds (requires API key)
  5. NFL.com (undocumented API)

Select source [1-5]:
```
- Note: Changing source at runtime requires rebuilding the DI container (or at minimum the scraper service registrations). We'll handle this by storing the selection and signaling a host rebuild when the user picks a scrape option.

### 5e. Input handling
- `Console.ReadLine()` for input
- Input validation with retry on invalid input
- `Ctrl+C` graceful exit handling (already partially handled by `Host.CreateDefaultBuilder`)

---

## Phase 6: Update Tests

### 6a. Update existing scraper tests
All tests that mock scraper methods need to account for the new `ScrapeResult` return type. The mock setups change from `ReturnsAsync(Task.CompletedTask)` style to `ReturnsAsync(new ScrapeResult { ... })`.

**Test files that reference scraper interfaces and may need updates:**
- `Scrapers/Espn/EspnTeamServiceTests.cs` (8 tests)
- `Scrapers/Espn/EspnGameServiceTests.cs` (7 tests)
- `Scrapers/SportsDataIo/SportsDataTeamServiceTests.cs` (6 tests)
- `Scrapers/MySportsFeeds/MySportsFeedsTeamServiceTests.cs` (7 tests)
- `Scrapers/NflCom/NflComTeamServiceTests.cs` (8 tests)
- `Services/DataProviderFactoryTests.cs` (9 tests — interface resolution)

### 6b. Add new tests
- `Models/ScrapeResultTests.cs` — default values, success/failure states
- `Services/ConsoleDisplayServiceTests.cs` — table formatting, banner output (capture Console output with StringWriter)
- `Integration/InteractiveModeTests.cs` — optional, depends on how testable the interactive loop is

### 6c. Run full test suite
`dotnet test` — verify all existing + new tests pass.

---

## Phase 7: Update CLAUDE.md

Update the project guide to reflect all changes:
- Add `ScrapeResult` to Models section
- Add `ConsoleDisplayService` to Services section
- Update `IScraperService` documentation for new return types
- Add `list` and `status` commands to CLI Commands section
- Add Interactive Mode section
- Update Project Structure tree with new files
- Update Implementation Status with new UI phases
- Update Test Coverage table with new test files

---

## Files Created (new)
| File | Purpose |
|------|---------|
| `Models/ScrapeResult.cs` | Scraper operation result model |
| `Services/ConsoleDisplayService.cs` | User-facing console output (tables, banners, progress) |

## Files Modified
| File | Changes |
|------|---------|
| `Services/Scrapers/IScraperService.cs` | All methods return `Task<ScrapeResult>` |
| `Program.cs` | Interactive mode, list/status commands, result handling, exit codes, banner, validation |
| 20 scraper implementations | Return `ScrapeResult` instead of void |
| Multiple test files | Account for `ScrapeResult` return type |
| `CLAUDE.md` | Full documentation update |

## Implementation Order
```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6 → Phase 7
  ↓          ↓          ↓          ↓          ↓          ↓         ↓
Result    Display    Wire up    Query      REPL     Verify    Docs
Model     Layer     Program    Commands    Loop     Tests
```

Each phase is independently buildable and testable. We can run `dotnet build` and `dotnet test` after each phase to ensure nothing breaks.
