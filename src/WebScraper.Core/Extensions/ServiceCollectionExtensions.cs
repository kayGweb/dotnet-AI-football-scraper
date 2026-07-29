using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebScraper.Data;
using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Services;
using WebScraper.Services.Agent;
using WebScraper.Services.Coverage;

namespace WebScraper.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all core services required by any host (CLI, Web API, MCP server, Worker):
    /// configuration binding, DbContext with the auditing interceptor, repositories,
    /// rate limiter, display/push services, and the selected data-provider scrapers.
    /// </summary>
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
        var connectionString = ResolveConnectionString(configuration, provider);

        // Auditing interceptor stamps CreatedAt/UpdatedAt and converts hard deletes to soft deletes
        services.AddSingleton<AuditingSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
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

            options.AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>());
        });

        // Register repositories
        services.AddScoped<IFranchiseRepository, FranchiseRepository>();
        services.AddScoped<ITeamSeasonRepository, TeamSeasonRepository>();
        services.AddScoped<IPlayerTeamSeasonRepository, PlayerTeamSeasonRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IStatsRepository, StatsRepository>();
        services.AddScoped<IVenueRepository, VenueRepository>();
        services.AddScoped<ITeamGameStatsRepository, TeamGameStatsRepository>();
        services.AddScoped<IInjuryRepository, InjuryRepository>();
        services.AddScoped<IApiLinkRepository, ApiLinkRepository>();

        // Register rate limiter, display service, and push service
        services.AddSingleton<RateLimiterService>();
        services.AddSingleton<ConsoleDisplayService>();
        services.AddScoped<BackfillOrchestrator>();
        services.AddScoped<SeasonCoverageService>();
        services.AddScoped<QualityRulesEngine>();
        services.AddScoped<RepairJobEnqueuer>();
        services.AddScoped<QueryStatsService>();
        services.AddScoped<DataCorrectionService>();
        services.AddScoped<GapFinderService>();

        // Register scraper services via provider factory (driven by DataProvider config)
        DataProviderFactory.RegisterScrapers(services, scraperSettings);

        return services;
    }

    /// <summary>
    /// Resolves the connection string, checking DATABASE_URL env var first for PostgreSQL.
    /// Converts URI format (postgresql://user:pass@host/db) to ADO.NET format for Npgsql.
    /// </summary>
    private static string? ResolveConnectionString(IConfiguration configuration, string provider)
    {
        // Check DATABASE_URL environment variable (standard for Neon/Vercel/Heroku)
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrEmpty(databaseUrl) && provider.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
        {
            return ConvertPostgresUri(databaseUrl);
        }

        return configuration.GetConnectionString("DefaultConnection");
    }

    /// <summary>
    /// Converts a PostgreSQL URI (postgresql://user:pass@host:port/db?params) to ADO.NET format.
    /// </summary>
    private static string ConvertPostgresUri(string uri)
    {
        // If it's already in ADO.NET format (contains "Host="), return as-is
        if (uri.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            return uri;

        var parsed = new Uri(uri);
        var userInfo = parsed.UserInfo.Split(':');
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var host = parsed.Host;
        var port = parsed.Port > 0 ? parsed.Port : 5432;
        var database = parsed.AbsolutePath.TrimStart('/');

        var sslMode = "Require";
        if (!string.IsNullOrEmpty(parsed.Query))
        {
            var queryParams = parsed.Query.TrimStart('?').Split('&');
            foreach (var param in queryParams)
            {
                var parts = param.Split('=', 2);
                if (parts.Length == 2 && parts[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                {
                    sslMode = parts[1].Equals("require", StringComparison.OrdinalIgnoreCase)
                        ? "Require" : parts[1];
                }
            }
        }

        return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode={sslMode}";
    }

    /// <summary>
    /// Expands a relative SQLite "Data Source" path against the repository root (nearest
    /// ancestor of the working directory containing a .sln or .git), so every host
    /// (CLI, API, MCP) opens the same database file no matter which project directory
    /// "dotnet run" starts it in. Outside a repo (e.g. a published deployment) it falls
    /// back to the working directory. Anchoring to AppContext.BaseDirectory would give
    /// each host a private DB buried in its own bin/ output — never shared, and wiped
    /// by "dotnet clean".
    /// </summary>
    public static string? ResolveSqlitePath(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return connectionString;

        const string prefix = "Data Source=";
        if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return connectionString;

        var path = connectionString[prefix.Length..].Trim();
        if (Path.IsPathRooted(path)) return connectionString;

        var anchor = FindRepositoryRoot() ?? Directory.GetCurrentDirectory();
        var absolutePath = Path.Combine(anchor, path);
        var dir = Path.GetDirectoryName(absolutePath);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        return $"{prefix}{absolutePath}";
    }

    private static string? FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (dir.EnumerateFiles("*.sln").Any() || Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
