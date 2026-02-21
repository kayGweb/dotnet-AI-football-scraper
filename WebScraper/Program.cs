using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using WebScraper.Data;
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

        default:
            display.PrintError($"Unknown command: '{command}'");
            PrintUsage();
            return 1;
    }

    return result != null && result.Success ? 0 : 1;
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

        Commands:
          teams                              Scrape all 32 NFL teams
          teams    --team <abbr>             Scrape a single team by NFL abbreviation
          players                            Scrape rosters for all teams
          games    --season <year>           Scrape full season schedule/scores
          games    --season <year> --week <n> Scrape games for a specific week
          stats    --season <year> --week <n> Scrape player stats for a week
          all      --season <year>           Run full pipeline (teams, players, games)

        Options:
          --team <abbr>       NFL team abbreviation (e.g., KC, NE, DAL)
          --season <year>     NFL season year (1920-current)
          --week <n>          Week number (1-22)
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
          dotnet run -- games --season 2025 --source SportsDataIo
        """);
}
