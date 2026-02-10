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
                    options.UseSqlite(connectionString);
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

        // Register rate limiter as singleton
        services.AddSingleton<RateLimiterService>();

        // Register scraper services via provider factory (driven by DataProvider config)
        DataProviderFactory.RegisterScrapers(services, scraperSettings);

        return services;
    }
}
