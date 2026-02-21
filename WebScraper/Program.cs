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
        var display = new ConsoleDisplayService();
        display.PrintError($"Unknown data provider: '{sourceOverride}'");
        display.PrintInfo(ConsoleDisplayService.GetValidProvidersMessage());
        return 1;
    }

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog((context, services, configuration) =>
        {
            configuration.ReadFrom.Configuration(context.Configuration);
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

// --- Command dispatch ---

static async Task<int> RunCommandAsync(IHost host, string[] args, ConsoleDisplayService display)
{
    if (args.Length == 0 || args[0].Equals("--help", StringComparison.OrdinalIgnoreCase)
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

static async Task<int> RunListCommandAsync(string[] args, IServiceProvider services, ConsoleDisplayService display, int? season, int? week)
{
    if (args.Length < 2)
    {
        display.PrintError("list requires a subcommand: teams, players, games, or stats");
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

        default:
            display.PrintError($"Unknown list subcommand: '{subcommand}'. Use: teams, players, games, stats");
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
