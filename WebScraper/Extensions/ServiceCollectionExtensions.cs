using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebScraper.Data;
using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Services;

namespace WebScraper.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebScraperServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind ScraperSettings from configuration
        var scraperSettings = new ScraperSettings();
        configuration.GetSection("ScraperSettings").Bind(scraperSettings);
        services.Configure<ScraperSettings>(configuration.GetSection("ScraperSettings"));

        // Configure database based on provider setting
        var provider = configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options =>
        {
            switch (provider.ToLowerInvariant())
            {
                case "sqlite":
                    var resolvedConn = ResolveSqlitePath(connectionString);
                    options.UseSqlite(resolvedConn);
                    break;
                case "postgresql":
                    options.UseNpgsql(connectionString);
                    break;
                case "sqlserver":
                    options.UseSqlServer(connectionString);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported database provider: {provider}");
            }
        });

        // Register repositories
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IStatsRepository, StatsRepository>();

        // Register rate limiter and display service as singletons
        services.AddSingleton<RateLimiterService>();
        services.AddSingleton<ConsoleDisplayService>();

        // Register scraper services via provider factory (driven by DataProvider config)
        DataProviderFactory.RegisterScrapers(services, scraperSettings);

        return services;
    }

    private static string? ResolveSqlitePath(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return connectionString;

        const string prefix = "Data Source=";
        if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return connectionString;

        var path = connectionString[prefix.Length..].Trim();
        if (Path.IsPathRooted(path)) return connectionString;

        var absolutePath = Path.Combine(AppContext.BaseDirectory, path);
        var dir = Path.GetDirectoryName(absolutePath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        return $"{prefix}{absolutePath}";
    }
}
