using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using WebScraper.Models;
using WebScraper.Services.Scrapers;
using WebScraper.Services.Scrapers.Espn;
using WebScraper.Services.Scrapers.MySportsFeeds;
using WebScraper.Services.Scrapers.SportsDataIo;

namespace WebScraper.Services;

public static class DataProviderFactory
{
    public static void RegisterScrapers(
        IServiceCollection services,
        ScraperSettings settings)
    {
        switch (settings.DataProvider.ToLowerInvariant())
        {
            case "profootballreference":
                AddScraperHttpClient<ITeamScraperService, TeamScraperService>(services, settings);
                AddScraperHttpClient<IPlayerScraperService, PlayerScraperService>(services, settings);
                AddScraperHttpClient<IGameScraperService, GameScraperService>(services, settings);
                AddScraperHttpClient<IStatsScraperService, StatsScraperService>(services, settings);
                break;

            case "espn":
                var espnSettings = settings.Providers.GetValueOrDefault("Espn") ?? new ApiProviderSettings();
                AddApiHttpClient<ITeamScraperService, EspnTeamService>(services, settings, espnSettings);
                AddApiHttpClient<IPlayerScraperService, EspnPlayerService>(services, settings, espnSettings);
                AddApiHttpClient<IGameScraperService, EspnGameService>(services, settings, espnSettings);
                AddApiHttpClient<IStatsScraperService, EspnStatsService>(services, settings, espnSettings);
                break;

            case "sportsdataio":
                var sportsDataSettings = settings.Providers.GetValueOrDefault("SportsDataIo") ?? new ApiProviderSettings();
                AddApiHttpClient<ITeamScraperService, SportsDataTeamService>(services, settings, sportsDataSettings);
                AddApiHttpClient<IPlayerScraperService, SportsDataPlayerService>(services, settings, sportsDataSettings);
                AddApiHttpClient<IGameScraperService, SportsDataGameService>(services, settings, sportsDataSettings);
                AddApiHttpClient<IStatsScraperService, SportsDataStatsService>(services, settings, sportsDataSettings);
                break;

            case "mysportsfeeds":
                var msfSettings = settings.Providers.GetValueOrDefault("MySportsFeeds") ?? new ApiProviderSettings();
                AddApiHttpClient<ITeamScraperService, MySportsFeedsTeamService>(services, settings, msfSettings);
                AddApiHttpClient<IPlayerScraperService, MySportsFeedsPlayerService>(services, settings, msfSettings);
                AddApiHttpClient<IGameScraperService, MySportsFeedsGameService>(services, settings, msfSettings);
                AddApiHttpClient<IStatsScraperService, MySportsFeedsStatsService>(services, settings, msfSettings);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported data provider: '{settings.DataProvider}'. " +
                    "Supported: ProFootballReference, Espn, SportsDataIo, MySportsFeeds");
        }
    }

    internal static void AddScraperHttpClient<TInterface, TImplementation>(
        IServiceCollection services,
        ScraperSettings settings)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.AddHttpClient<TInterface, TImplementation>()
            .AddResilienceHandler($"{typeof(TImplementation).Name}-pipeline", builder =>
            {
                ConfigureResiliencePipeline<TImplementation>(builder, services, settings);
            });
    }

    internal static void AddApiHttpClient<TInterface, TImplementation>(
        IServiceCollection services,
        ScraperSettings settings,
        ApiProviderSettings providerSettings)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.AddSingleton(providerSettings);

        services.AddHttpClient<TInterface, TImplementation>(client =>
            {
                if (!string.IsNullOrEmpty(providerSettings.BaseUrl))
                {
                    client.BaseAddress = new Uri(providerSettings.BaseUrl);
                }

                client.DefaultRequestHeaders.UserAgent.ParseAdd(settings.UserAgent);
                client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
            })
            .AddResilienceHandler($"{typeof(TImplementation).Name}-pipeline", builder =>
            {
                ConfigureResiliencePipeline<TImplementation>(builder, services, settings);
            });
    }

    private static void ConfigureResiliencePipeline<TImplementation>(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        IServiceCollection services,
        ScraperSettings settings)
    {
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

        builder.AddCircuitBreaker(new Polly.CircuitBreaker.HttpCircuitBreakerStrategyOptions
        {
            SamplingDuration = TimeSpan.FromSeconds(30),
            FailureRatio = 0.7,
            MinimumThroughput = 3,
            BreakDuration = TimeSpan.FromSeconds(15)
        });

        builder.AddTimeout(TimeSpan.FromSeconds(settings.TimeoutSeconds));
    }
}
