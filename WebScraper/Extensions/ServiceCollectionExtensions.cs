using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using WebScraper.Data;
using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Services;
using WebScraper.Services.Scrapers;

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

        // Register scraper services with typed HttpClient + Polly retry
        AddScraperHttpClient<ITeamScraperService, TeamScraperService>(services, scraperSettings);
        AddScraperHttpClient<IPlayerScraperService, PlayerScraperService>(services, scraperSettings);
        AddScraperHttpClient<IGameScraperService, GameScraperService>(services, scraperSettings);
        AddScraperHttpClient<IStatsScraperService, StatsScraperService>(services, scraperSettings);

        return services;
    }

    private static void AddScraperHttpClient<TInterface, TImplementation>(
        IServiceCollection services,
        ScraperSettings settings)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.AddHttpClient<TInterface, TImplementation>()
            .AddResilienceHandler($"{typeof(TImplementation).Name}-pipeline", builder =>
            {
                // Retry on transient HTTP errors (5xx, 408, 429) with exponential backoff
                builder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = settings.MaxRetries,
                    BackoffType = DelayBackoffType.Exponential,
                    Delay = TimeSpan.FromSeconds(2),
                    ShouldHandle = args => ValueTask.FromResult(
                        args.Outcome.Result?.StatusCode is
                            HttpStatusCode.RequestTimeout or
                            HttpStatusCode.TooManyRequests or
                            HttpStatusCode.InternalServerError or
                            HttpStatusCode.BadGateway or
                            HttpStatusCode.ServiceUnavailable or
                            HttpStatusCode.GatewayTimeout
                        || args.Outcome.Exception is HttpRequestException or TaskCanceledException),
                    OnRetry = args =>
                    {
                        var serviceProvider = services.BuildServiceProvider();
                        var logger = serviceProvider.GetService<ILogger<BaseScraperService>>();
                        logger?.LogWarning(
                            "Retry {AttemptNumber} for {PipelineName} after {Delay}s. Status: {StatusCode}",
                            args.AttemptNumber,
                            typeof(TImplementation).Name,
                            args.RetryDelay.TotalSeconds,
                            args.Outcome.Result?.StatusCode);
                        return ValueTask.CompletedTask;
                    }
                });

                // Circuit breaker — stop hitting a server that keeps failing
                builder.AddCircuitBreaker(new Polly.CircuitBreaker.HttpCircuitBreakerStrategyOptions
                {
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    FailureRatio = 0.7,
                    MinimumThroughput = 3,
                    BreakDuration = TimeSpan.FromSeconds(15)
                });

                // Overall timeout per request attempt
                builder.AddTimeout(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            });
    }
}
