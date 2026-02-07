using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using WebScraper.Data;
using WebScraper.Extensions;
using WebScraper.Services.Scrapers;

// Configure Serilog from appsettings.json
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting NFL Web Scraper");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog((context, services, configuration) =>
        {
            configuration.ReadFrom.Configuration(context.Configuration);
        })
        .ConfigureServices((context, services) =>
        {
            services.AddWebScraperServices(context.Configuration);
        })
        .Build();

    // Apply pending migrations (creates DB if it doesn't exist)
    using (var scope = host.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        Log.Information("Database migrated and ready");
    }

    // Parse CLI arguments and dispatch
    await RunCommandAsync(host, args);
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

return 0;

// --- Command dispatch ---

static async Task RunCommandAsync(IHost host, string[] args)
{
    if (args.Length == 0 || args[0].Equals("--help", StringComparison.OrdinalIgnoreCase)
                        || args[0].Equals("-h", StringComparison.OrdinalIgnoreCase))
    {
        PrintUsage();
        return;
    }

    var command = args[0].ToLowerInvariant();
    var season = GetArgValue(args, "--season");
    var week = GetArgValue(args, "--week");

    // Validate season range
    if (season != null && (season.Value < 1920 || season.Value > DateTime.Now.Year + 1))
    {
        Log.Error("Invalid season {Season}. Must be between 1920 and {MaxYear}", season.Value, DateTime.Now.Year + 1);
        return;
    }

    // Validate week range
    if (week != null && (week.Value < 1 || week.Value > 22))
    {
        Log.Error("Invalid week {Week}. Must be between 1 and 22", week.Value);
        return;
    }

    using var scope = host.Services.CreateScope();
    var services = scope.ServiceProvider;

    switch (command)
    {
        case "teams":
            var teamScraper = services.GetRequiredService<ITeamScraperService>();
            await teamScraper.ScrapeTeamsAsync();
            break;

        case "players":
            var playerScraper = services.GetRequiredService<IPlayerScraperService>();
            await playerScraper.ScrapeAllPlayersAsync();
            break;

        case "games":
            if (season == null)
            {
                Log.Error("--season is required for games command");
                PrintUsage();
                return;
            }
            var gameScraper = services.GetRequiredService<IGameScraperService>();
            if (week != null)
                await gameScraper.ScrapeGamesAsync(season.Value, week.Value);
            else
                await gameScraper.ScrapeGamesAsync(season.Value);
            break;

        case "stats":
            if (season == null || week == null)
            {
                Log.Error("--season and --week are required for stats command");
                PrintUsage();
                return;
            }
            var statsScraper = services.GetRequiredService<IStatsScraperService>();
            await statsScraper.ScrapePlayerStatsAsync(season.Value, week.Value);
            break;

        case "all":
            if (season == null)
            {
                Log.Error("--season is required for all command");
                PrintUsage();
                return;
            }
            await RunAllAsync(services, season.Value);
            break;

        default:
            Log.Error("Unknown command: {Command}", command);
            PrintUsage();
            break;
    }
}

static async Task RunAllAsync(IServiceProvider services, int season)
{
    Log.Information("Running full scrape pipeline for season {Season}", season);

    var teamScraper = services.GetRequiredService<ITeamScraperService>();
    await teamScraper.ScrapeTeamsAsync();

    var playerScraper = services.GetRequiredService<IPlayerScraperService>();
    await playerScraper.ScrapeAllPlayersAsync();

    var gameScraper = services.GetRequiredService<IGameScraperService>();
    await gameScraper.ScrapeGamesAsync(season);

    Log.Information("Full scrape pipeline complete for season {Season}", season);
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

static void PrintUsage()
{
    Console.WriteLine("""
        NFL Web Scraper v1.0

        Usage: dotnet run -- <command> [options]

        Commands:
          teams                              Scrape all 32 NFL teams
          players                            Scrape rosters for all teams
          games    --season <year>           Scrape full season schedule/scores
          games    --season <year> --week <n> Scrape games for a specific week
          stats    --season <year> --week <n> Scrape player stats for a week
          all      --season <year>           Run full pipeline (teams, players, games)

        Options:
          --season <year>   NFL season year (1920-current)
          --week <n>        Week number (1-22)
          --help, -h        Show this help message

        Examples:
          dotnet run -- teams
          dotnet run -- games --season 2025
          dotnet run -- stats --season 2025 --week 1
          dotnet run -- all --season 2025
        """);
}
