using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using WebScraper.Data;
using WebScraper.Data.Repositories;
using WebScraper.Extensions;
using WebScraper.Models;
using WebScraper.Services;
using WebScraper.Services.Scrapers;

// Configure Serilog from appsettings.json
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    // Pre-parse --source flag to override DataProvider before host is built
    var sourceOverride = GetStringArgValue(args, "--source");

    // Early validation of --source flag before building DI container
    if (sourceOverride != null && !ConsoleDisplayService.IsValidProvider(sourceOverride))
    {
        var earlyDisplay = new ConsoleDisplayService();
        earlyDisplay.PrintError($"Unknown data provider: '{sourceOverride}'");
        earlyDisplay.PrintInfo(ConsoleDisplayService.GetValidProvidersMessage());
        return 1;
    }

    // Interactive mode: no args or explicit "interactive" command
    if (args.Length == 0 || args[0].Equals("interactive", StringComparison.OrdinalIgnoreCase))
    {
        return await RunInteractiveAsync(sourceOverride);
    }

    // CLI mode
    var host = BuildHost(args, sourceOverride);

    // Read config values for banner
    var configuration = host.Services.GetRequiredService<IConfiguration>();
    var dataProvider = configuration.GetValue<string>("ScraperSettings:DataProvider") ?? "ProFootballReference";
    var dbProvider = configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
    var connString = configuration.GetConnectionString("DefaultConnection") ?? "";

    // Print startup banner
    var display = host.Services.GetRequiredService<ConsoleDisplayService>();
    display.PrintBanner(dataProvider, dbProvider, connString);

    // Apply pending migrations (creates DB if it doesn't exist)
    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        Log.Information("Database migrated and ready");
    }

    // Connectivity check for API-based providers
    await CheckApiConnectivityAsync(host, dataProvider, display);

    // Parse CLI arguments and dispatch
    return await RunCommandAsync(host, args, display);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

// --- Host builder ---

static IHost BuildHost(string[] cliArgs, string? sourceOverride)
{
    var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
    if (!Directory.Exists(logDir))
        Directory.CreateDirectory(logDir);
    var logPath = Path.Combine(logDir, "scraper-.log");

    return Host.CreateDefaultBuilder(cliArgs)
        .UseContentRoot(AppContext.BaseDirectory)
        .UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .WriteTo.File(logPath, rollingInterval: Serilog.RollingInterval.Day);
        })
        .ConfigureAppConfiguration((context, config) =>
        {
            if (sourceOverride != null)
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ScraperSettings:DataProvider"] = sourceOverride
                });
            }
        })
        .ConfigureServices((context, services) =>
        {
            services.AddWebScraperServices(context.Configuration);
        })
        .Build();
}

// --- API connectivity check ---

static async Task CheckApiConnectivityAsync(IHost host, string dataProvider, ConsoleDisplayService display)
{
    if (dataProvider.Equals("ProFootballReference", StringComparison.OrdinalIgnoreCase))
        return;

    try
    {
        using var scope = host.Services.CreateScope();
        var teamService = scope.ServiceProvider.GetRequiredService<ITeamScraperService>();

        if (teamService is BaseApiService apiService)
        {
            var reachable = await apiService.CheckConnectivityAsync();
            if (!reachable)
            {
                var message =
                    $"{ConsoleDisplayService.GetProviderDisplayName(dataProvider)} may be unreachable. " +
                    "Scraping commands may fail.";
                display.PrintWarning(message);
                Log.Warning("API connectivity check: {Message}", message);
            }
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not perform API connectivity check");
    }
}

// --- CLI command dispatch ---

static async Task<int> RunCommandAsync(IHost host, string[] args, ConsoleDisplayService display)
{
    if (args[0].Equals("--help", StringComparison.OrdinalIgnoreCase)
        || args[0].Equals("-h", StringComparison.OrdinalIgnoreCase))
    {
        PrintUsage();
        return 0;
    }

    var command = args[0].ToLowerInvariant();
    var season = GetArgValue(args, "--season");
    var week = GetArgValue(args, "--week");

    // Validate season range
    if (season != null && (season.Value < 1920 || season.Value > DateTime.Now.Year + 1))
    {
        display.PrintError($"Invalid season {season.Value}. Must be between 1920 and {DateTime.Now.Year + 1}");
        return 1;
    }

    // Validate week range
    if (week != null && (week.Value < 1 || week.Value > 22))
    {
        display.PrintError($"Invalid week {week.Value}. Must be between 1 and 22");
        return 1;
    }

    using var scope = host.Services.CreateScope();
    var services = scope.ServiceProvider;
    ScrapeResult? result = null;

    switch (command)
    {
        case "teams":
            var teamAbbr = GetStringArgValue(args, "--team");
            var teamScraper = services.GetRequiredService<ITeamScraperService>();
            result = teamAbbr != null
                ? await teamScraper.ScrapeTeamAsync(teamAbbr)
                : await teamScraper.ScrapeTeamsAsync();
            display.PrintScrapeResult("Teams", result);
            break;

        case "players":
            var playerScraper = services.GetRequiredService<IPlayerScraperService>();
            result = await playerScraper.ScrapeAllPlayersAsync();
            display.PrintScrapeResult("Players", result);
            break;

        case "games":
            if (season == null)
            {
                display.PrintError("--season is required for games command");
                PrintUsage();
                return 1;
            }
            var gameScraper = services.GetRequiredService<IGameScraperService>();
            result = week != null
                ? await gameScraper.ScrapeGamesAsync(season.Value, week.Value)
                : await gameScraper.ScrapeGamesAsync(season.Value);
            display.PrintScrapeResult("Games", result);
            break;

        case "stats":
            if (season == null || week == null)
            {
                display.PrintError("--season and --week are required for stats command");
                PrintUsage();
                return 1;
            }
            var statsScraper = services.GetRequiredService<IStatsScraperService>();
            result = await statsScraper.ScrapePlayerStatsAsync(season.Value, week.Value);
            display.PrintScrapeResult("Stats", result);
            break;

        case "all":
            if (season == null)
            {
                display.PrintError("--season is required for all command");
                PrintUsage();
                return 1;
            }
            result = await RunAllAsync(services, season.Value, display);
            break;

        case "list":
            return await RunListCommandAsync(args, services, display, season, week);

        case "status":
            await RunStatusCommandAsync(services, display);
            return 0;

        default:
            display.PrintError($"Unknown command: '{command}'");
            PrintUsage();
            return 1;
    }

    return result != null && result.Success ? 0 : 1;
}

// --- Data display commands ---

static async Task<int> RunListCommandAsync(string[] args, IServiceProvider services, ConsoleDisplayService display, int? season, int? week)
{
    if (args.Length < 2)
    {
        display.PrintError("list requires a subcommand: teams, players, games, stats, or abbr");
        PrintUsage();
        return 1;
    }

    var subcommand = args[1].ToLowerInvariant();

    switch (subcommand)
    {
        case "teams":
            var conference = GetStringArgValue(args, "--conference");
            var teamRepo = services.GetRequiredService<ITeamRepository>();

            if (conference != null)
            {
                var confUpper = conference.ToUpperInvariant();
                if (confUpper != "AFC" && confUpper != "NFC")
                {
                    display.PrintError("--conference must be AFC or NFC");
                    return 1;
                }
                var confTeams = await teamRepo.GetByConferenceAsync(confUpper);
                display.PrintTeamsTable(confTeams);
            }
            else
            {
                var allTeams = await teamRepo.GetAllAsync();
                display.PrintTeamsTable(allTeams);
            }
            return 0;

        case "players":
            var teamAbbr = GetStringArgValue(args, "--team");
            var playerRepo = services.GetRequiredService<IPlayerRepository>();

            if (teamAbbr != null)
            {
                var teamRepoForLookup = services.GetRequiredService<ITeamRepository>();
                var team = await teamRepoForLookup.GetByAbbreviationAsync(teamAbbr.ToUpperInvariant());
                if (team == null)
                {
                    display.PrintError($"Team '{teamAbbr}' not found in database. Run 'teams' scrape first.");
                    return 1;
                }
                var teamPlayers = await playerRepo.GetByTeamAsync(team.Id);
                display.PrintPlayersTable(teamPlayers, $"{team.City} {team.Name}");
            }
            else
            {
                var allPlayers = await playerRepo.GetAllAsync();
                display.PrintPlayersTable(allPlayers);
            }
            return 0;

        case "games":
            if (season == null)
            {
                display.PrintError("--season is required for list games");
                return 1;
            }
            var gameRepo = services.GetRequiredService<IGameRepository>();
            var games = week != null
                ? await gameRepo.GetByWeekAsync(season.Value, week.Value)
                : await gameRepo.GetBySeasonAsync(season.Value);
            display.PrintGamesTable(games, season, week);
            return 0;

        case "stats":
            var playerName = GetStringArgValue(args, "--player");
            var statsRepo = services.GetRequiredService<IStatsRepository>();

            if (playerName != null && season != null)
            {
                var playerStats = await statsRepo.GetPlayerStatsAsync(playerName, season.Value);
                display.PrintInfo($"Stats for {playerName} — {season.Value} Season");
                display.PrintStatsTable(playerStats);
            }
            else if (season != null && week != null)
            {
                var gameRepoForStats = services.GetRequiredService<IGameRepository>();
                var weekGames = await gameRepoForStats.GetByWeekAsync(season.Value, week.Value);
                var allStats = new List<PlayerGameStats>();
                foreach (var game in weekGames)
                {
                    var gameStats = await statsRepo.GetGameStatsAsync(game.Id);
                    allStats.AddRange(gameStats);
                }
                display.PrintInfo($"Stats — {season.Value} Season, Week {week.Value}");
                display.PrintStatsTable(allStats);
            }
            else
            {
                display.PrintError("list stats requires --season and --week, or --player and --season");
                return 1;
            }
            return 0;

        case "abbr":
        case "abbreviations":
            display.PrintAbbreviationsTable();
            return 0;

        default:
            display.PrintError($"Unknown list subcommand: '{subcommand}'. Use: teams, players, games, stats, abbr");
            return 1;
    }
}

static async Task RunStatusCommandAsync(IServiceProvider services, ConsoleDisplayService display)
{
    var teamRepo = services.GetRequiredService<ITeamRepository>();
    var playerRepo = services.GetRequiredService<IPlayerRepository>();
    var gameRepo = services.GetRequiredService<IGameRepository>();
    var statsRepo = services.GetRequiredService<IStatsRepository>();

    var teams = (await teamRepo.GetAllAsync()).Count();
    var players = (await playerRepo.GetAllAsync()).Count();
    var games = (await gameRepo.GetAllAsync()).Count();
    var stats = (await statsRepo.GetAllAsync()).Count();

    display.PrintDatabaseStatus(teams, players, games, stats);
}

// --- Scrape pipeline ---

static async Task<ScrapeResult> RunAllAsync(IServiceProvider services, int season, ConsoleDisplayService display)
{
    display.PrintInfo($"Running full scrape pipeline for season {season}...");
    Console.WriteLine();
    var errors = new List<string>();
    int totalRecords = 0;

    // Step 1: Teams
    var teamScraper = services.GetRequiredService<ITeamScraperService>();
    var teamResult = await teamScraper.ScrapeTeamsAsync();
    display.PrintScrapeResult("Teams", teamResult);
    totalRecords += teamResult.RecordsProcessed;

    if (!teamResult.Success)
    {
        display.PrintError("Teams scrape failed. Cannot continue pipeline.");
        return ScrapeResult.Failed("Pipeline stopped: teams scrape failed");
    }

    // Step 2: Players
    var playerScraper = services.GetRequiredService<IPlayerScraperService>();
    var playerResult = await playerScraper.ScrapeAllPlayersAsync();
    display.PrintScrapeResult("Players", playerResult);
    totalRecords += playerResult.RecordsProcessed;

    if (!playerResult.Success)
        errors.Add(playerResult.Message);

    // Step 3: Games
    var gameScraper = services.GetRequiredService<IGameScraperService>();
    var gameResult = await gameScraper.ScrapeGamesAsync(season);
    display.PrintScrapeResult("Games", gameResult);
    totalRecords += gameResult.RecordsProcessed;

    if (!gameResult.Success)
        errors.Add(gameResult.Message);

    Console.WriteLine();
    display.PrintInfo($"Full pipeline complete for season {season}. {totalRecords} total records processed.");

    return new ScrapeResult
    {
        Success = errors.Count == 0,
        RecordsProcessed = totalRecords,
        Message = $"Pipeline complete: {totalRecords} records across teams, players, and games",
        Errors = errors
    };
}

// --- Interactive mode ---

static async Task<int> RunInteractiveAsync(string? initialSource)
{
    string? currentSource = initialSource;

    while (true)
    {
        using var host = BuildHost(Array.Empty<string>(), currentSource);

        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var dataProvider = configuration.GetValue<string>("ScraperSettings:DataProvider") ?? "ProFootballReference";
        var dbProvider = configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
        var connString = configuration.GetConnectionString("DefaultConnection") ?? "";

        var display = host.Services.GetRequiredService<ConsoleDisplayService>();
        display.PrintBanner(dataProvider, dbProvider, connString);

        // Apply pending migrations
        using (var migrationScope = host.Services.CreateScope())
        {
            var db = migrationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }

        // Connectivity check for API-based providers
        await CheckApiConnectivityAsync(host, dataProvider, display);

        var sourceChanged = false;

        while (!sourceChanged)
        {
            display.PrintMainMenu(dataProvider);
            Console.Write("  > ");
            var input = Console.ReadLine()?.Trim();

            // Handle EOF (Ctrl+D / Ctrl+Z)
            if (input == null)
            {
                Console.WriteLine();
                display.PrintSuccess("Goodbye!");
                return 0;
            }

            if (string.IsNullOrEmpty(input)) continue;

            switch (input)
            {
                case "1":
                    using (var scope = host.Services.CreateScope())
                        await HandleScrapeMenuAsync(scope.ServiceProvider, display);
                    break;

                case "2":
                    using (var scope = host.Services.CreateScope())
                        await HandleViewMenuAsync(scope.ServiceProvider, display);
                    break;

                case "3":
                    using (var scope = host.Services.CreateScope())
                        await RunStatusCommandAsync(scope.ServiceProvider, display);
                    break;

                case "4":
                    var newSource = HandleChangeSource(display, dataProvider);
                    if (newSource != null)
                    {
                        currentSource = newSource;
                        sourceChanged = true;
                    }
                    break;

                case "5":
                    display.PrintSuccess("Goodbye!");
                    return 0;

                default:
                    display.PrintWarning("Invalid choice. Enter 1-5.");
                    break;
            }
        }
    }
}

static async Task HandleScrapeMenuAsync(IServiceProvider services, ConsoleDisplayService display)
{
    display.PrintScrapeMenu();
    Console.Write("  > ");
    var input = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(input) || input == "8") return;

    switch (input)
    {
        case "1":
            var teamScraper = services.GetRequiredService<ITeamScraperService>();
            var teamResult = await teamScraper.ScrapeTeamsAsync();
            display.PrintScrapeResult("Teams", teamResult);
            break;

        case "2":
            var abbr = PromptForAbbreviation(display);
            if (string.IsNullOrEmpty(abbr)) break;
            var singleTeamScraper = services.GetRequiredService<ITeamScraperService>();
            var singleResult = await singleTeamScraper.ScrapeTeamAsync(abbr);
            display.PrintScrapeResult("Team", singleResult);
            break;

        case "3":
            var playerScraper = services.GetRequiredService<IPlayerScraperService>();
            var playerResult = await playerScraper.ScrapeAllPlayersAsync();
            display.PrintScrapeResult("Players", playerResult);
            break;

        case "4":
            var season4 = PromptForInt("Season year (e.g., 2025)");
            if (season4 == null) break;
            var gameScraper4 = services.GetRequiredService<IGameScraperService>();
            var gameResult4 = await gameScraper4.ScrapeGamesAsync(season4.Value);
            display.PrintScrapeResult("Games", gameResult4);
            break;

        case "5":
            var season5 = PromptForInt("Season year (e.g., 2025)");
            if (season5 == null) break;
            var week5 = PromptForInt("Week number (1-22)");
            if (week5 == null) break;
            var gameScraper5 = services.GetRequiredService<IGameScraperService>();
            var gameResult5 = await gameScraper5.ScrapeGamesAsync(season5.Value, week5.Value);
            display.PrintScrapeResult("Games", gameResult5);
            break;

        case "6":
            var season6 = PromptForInt("Season year (e.g., 2025)");
            if (season6 == null) break;
            var week6 = PromptForInt("Week number (1-22)");
            if (week6 == null) break;
            var statsScraper = services.GetRequiredService<IStatsScraperService>();
            var statsResult = await statsScraper.ScrapePlayerStatsAsync(season6.Value, week6.Value);
            display.PrintScrapeResult("Stats", statsResult);
            break;

        case "7":
            var season7 = PromptForInt("Season year (e.g., 2025)");
            if (season7 == null) break;
            await RunAllAsync(services, season7.Value, display);
            break;

        default:
            display.PrintWarning("Invalid choice. Enter 1-8.");
            break;
    }

    Console.WriteLine();
}

static async Task HandleViewMenuAsync(IServiceProvider services, ConsoleDisplayService display)
{
    display.PrintViewMenu();
    Console.Write("  > ");
    var input = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(input) || input == "5") return;

    switch (input)
    {
        case "1":
            var teamRepo = services.GetRequiredService<ITeamRepository>();
            var teams = await teamRepo.GetAllAsync();
            display.PrintTeamsTable(teams);
            break;

        case "2":
            var teamAbbr = PromptForOptionalAbbreviation(display);
            var playerRepo = services.GetRequiredService<IPlayerRepository>();
            if (!string.IsNullOrEmpty(teamAbbr))
            {
                var teamRepoLookup = services.GetRequiredService<ITeamRepository>();
                var team = await teamRepoLookup.GetByAbbreviationAsync(teamAbbr);
                if (team == null)
                {
                    display.PrintError($"Team '{teamAbbr}' not found. Run teams scrape first.");
                    break;
                }
                var teamPlayers = await playerRepo.GetByTeamAsync(team.Id);
                display.PrintPlayersTable(teamPlayers, $"{team.City} {team.Name}");
            }
            else
            {
                var allPlayers = await playerRepo.GetAllAsync();
                display.PrintPlayersTable(allPlayers);
            }
            break;

        case "3":
            var gameSeason = PromptForInt("Season year (e.g., 2025)");
            if (gameSeason == null) break;
            var gameWeek = PromptForOptionalInt("Week number (1-22, or press Enter for all)");
            var gameRepo = services.GetRequiredService<IGameRepository>();
            var games = gameWeek != null
                ? await gameRepo.GetByWeekAsync(gameSeason.Value, gameWeek.Value)
                : await gameRepo.GetBySeasonAsync(gameSeason.Value);
            display.PrintGamesTable(games, gameSeason, gameWeek);
            break;

        case "4":
            Console.Write("  Player name (or press Enter for weekly stats): ");
            var playerName = Console.ReadLine()?.Trim();
            var statsRepo = services.GetRequiredService<IStatsRepository>();

            if (!string.IsNullOrEmpty(playerName))
            {
                var statsSeason = PromptForInt("Season year (e.g., 2025)");
                if (statsSeason == null) break;
                var playerStats = await statsRepo.GetPlayerStatsAsync(playerName, statsSeason.Value);
                display.PrintInfo($"Stats for {playerName} — {statsSeason.Value} Season");
                display.PrintStatsTable(playerStats);
            }
            else
            {
                var statsSeason = PromptForInt("Season year (e.g., 2025)");
                if (statsSeason == null) break;
                var statsWeek = PromptForInt("Week number (1-22)");
                if (statsWeek == null) break;
                var gameRepoForStats = services.GetRequiredService<IGameRepository>();
                var weekGames = await gameRepoForStats.GetByWeekAsync(statsSeason.Value, statsWeek.Value);
                var allStats = new List<PlayerGameStats>();
                foreach (var game in weekGames)
                {
                    var gameStats = await statsRepo.GetGameStatsAsync(game.Id);
                    allStats.AddRange(gameStats);
                }
                display.PrintInfo($"Stats — {statsSeason.Value} Season, Week {statsWeek.Value}");
                display.PrintStatsTable(allStats);
            }
            break;

        default:
            display.PrintWarning("Invalid choice. Enter 1-5.");
            break;
    }
}

static string? HandleChangeSource(ConsoleDisplayService display, string currentSource)
{
    display.PrintSourceMenu(currentSource);
    Console.Write("  > ");
    var input = Console.ReadLine()?.Trim();

    var newSource = input switch
    {
        "1" => "ProFootballReference",
        "2" => "Espn",
        "3" => "SportsDataIo",
        "4" => "MySportsFeeds",
        "5" => "NflCom",
        _ => null
    };

    if (newSource != null && !newSource.Equals(currentSource, StringComparison.OrdinalIgnoreCase))
    {
        display.PrintSuccess($"Source changed to {ConsoleDisplayService.GetProviderDisplayName(newSource)}. Rebuilding...");
        Console.WriteLine();
        return newSource;
    }

    if (newSource != null)
        display.PrintInfo($"Already using {ConsoleDisplayService.GetProviderDisplayName(newSource)}.");

    return null;
}

// --- Input helpers ---

static int? PromptForInt(string prompt)
{
    Console.Write($"  {prompt}: ");
    var input = Console.ReadLine()?.Trim();
    if (int.TryParse(input, out var value))
        return value;
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("  [WARN] ");
    Console.ResetColor();
    Console.WriteLine("Invalid number entered.");
    return null;
}

static int? PromptForOptionalInt(string prompt)
{
    Console.Write($"  {prompt}: ");
    var input = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(input))
        return null;
    if (int.TryParse(input, out var value))
        return value;
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("  [WARN] ");
    Console.ResetColor();
    Console.WriteLine("Invalid number entered.");
    return null;
}

static string? PromptForAbbreviation(ConsoleDisplayService display)
{
    while (true)
    {
        Console.Write("  Team abbreviation (e.g., KC — enter ? to list all): ");
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(input))
        {
            display.PrintWarning("No abbreviation entered.");
            return null;
        }
        if (input == "?")
        {
            display.PrintAbbreviationsTable();
            continue;
        }
        return input.ToUpperInvariant();
    }
}

static string? PromptForOptionalAbbreviation(ConsoleDisplayService display)
{
    while (true)
    {
        Console.Write("  Team abbreviation (e.g., KC — enter ? to list, Enter for all): ");
        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(input))
            return null;
        if (input == "?")
        {
            display.PrintAbbreviationsTable();
            continue;
        }
        return input.ToUpperInvariant();
    }
}

// --- CLI argument helpers ---

static int? GetArgValue(string[] args, string flag)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(args[i + 1], out var value))
                return value;
        }
    }
    return null;
}

static string? GetStringArgValue(string[] args, string flag)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
        {
            var value = args[i + 1];
            if (!string.IsNullOrWhiteSpace(value) && !value.StartsWith("--"))
                return value;
        }
    }
    return null;
}

static void PrintUsage()
{
    Console.WriteLine("""

        Usage: dotnet run -- <command> [options]

        Modes:
          (no args)                          Launch interactive mode
          interactive                        Launch interactive mode

        Scrape Commands:
          teams                              Scrape all 32 NFL teams
          teams    --team <abbr>             Scrape a single team by NFL abbreviation
          players                            Scrape rosters for all teams
          games    --season <year>           Scrape full season schedule/scores
          games    --season <year> --week <n> Scrape games for a specific week
          stats    --season <year> --week <n> Scrape player stats for a week
          all      --season <year>           Run full pipeline (teams, players, games)

        View Commands:
          list teams                         Show all teams in the database
          list teams --conference <AFC|NFC>  Show teams by conference
          list players                       Show all players
          list players --team <abbr>         Show roster for a team
          list games --season <year>         Show games for a season
          list games --season <year> --week <n>  Show games for a specific week
          list stats --season <year> --week <n>  Show player stats for a week
          list stats --player <name> --season <year>  Show a player's season stats
          list abbr                          Show all 32 NFL team abbreviations
          status                             Show database record counts

        Options:
          --team <abbr>       NFL team abbreviation (e.g., KC, NE, DAL)
          --season <year>     NFL season year (1920-current)
          --week <n>          Week number (1-22)
          --conference <conf> Conference filter (AFC or NFC)
          --player <name>     Player name for stats lookup
          --source <provider> Data source override (default: from appsettings.json)
                              Values: ProFootballReference, Espn, SportsDataIo,
                                      MySportsFeeds, NflCom
          --help, -h          Show this help message

        Examples:
          dotnet run                                                    # Interactive mode
          dotnet run -- teams
          dotnet run -- teams --team KC
          dotnet run -- games --season 2025
          dotnet run -- stats --season 2025 --week 1
          dotnet run -- all --season 2025
          dotnet run -- teams --source Espn
          dotnet run -- list teams
          dotnet run -- list players --team KC
          dotnet run -- list games --season 2025 --week 1
          dotnet run -- list stats --player "Patrick Mahomes" --season 2025
          dotnet run -- status
        """);
}
