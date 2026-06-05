using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MudBlazor.Services;
using WebScraper.Api.Auth;
using WebScraper.Api.Services;
using WebScraper.Data;

namespace WebScraper.Api.Extensions;

/// <summary>
/// Wires up API-host-specific services (auth, query logging, Swagger). Core
/// services (DbContext, repositories, scrapers) come from
/// <see cref="WebScraper.Extensions.ServiceCollectionExtensions.AddWebScraperServices"/>.
/// </summary>
public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Name of the default "smart" policy scheme that forwards each request to the
    /// concrete scheme (cookie / JWT / API key) appropriate for its path.
    /// </summary>
    private const string SmartSchemeName = "Smart";

    public static IServiceCollection AddWebScraperApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Identity (admin users + roles) ---
        services.AddIdentityInfrastructure(configuration);

        // --- API key + JWT auth (multi-scheme) ---
        services.AddApiAuthentication(configuration);

        services.AddAuthorization(options => options.AddWebScraperApiAuthorization());

        // --- API key management service ---
        services.AddScoped<ApiKeyManagementService>();

        // --- Query log queue + background writer ---
        services.AddSingleton<ApiQueryLogQueue>();
        services.AddSingleton<IApiQueryLogQueue>(sp => sp.GetRequiredService<ApiQueryLogQueue>());
        services.AddHostedService<ApiQueryLogWriter>();

        // --- Scrape job queue + background worker (M3 chunk b) ---
        services.AddSingleton<JobQueue>();
        services.AddSingleton<IJobQueue>(sp => sp.GetRequiredService<JobQueue>());
        services.AddHostedService<ScrapeJobWorker>();

        // --- SignalR hub + outbox relay (M3 chunk c) ---
        services.AddSignalR();
        services.AddHostedService<ScrapeEventRelay>();

        // --- Blazor admin dashboard (M4) ---
        services.AddRazorComponents().AddInteractiveServerComponents();
        services.AddMudServices();
        services.AddCascadingAuthenticationState();

        // --- Expose HttpContext to services that need claim lookups ---
        services.AddHttpContextAccessor();

        // --- Swagger / OpenAPI ---
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "WebScraper API",
                Version = "v1",
                Description =
                    "REST API over the NFL data store. Read endpoints accept an API key via X-Api-Key; " +
                    "admin endpoints accept a JWT bearer token issued by /api/v1/auth/login. " +
                    "Pagination defaults to page=1, pageSize=25 (max 200). Responses include the " +
                    "X-Total-Count header for list endpoints.",
            });

            // API key scheme
            options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Name = ApiKeyAuthenticationHandler.HeaderName,
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "API key issued via the admin dashboard (SHA-256 hashed at rest).",
            });

            // JWT bearer scheme
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT issued by POST /api/v1/auth/login. Header: 'Authorization: Bearer {token}'.",
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" },
                }] = Array.Empty<string>(),
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                }] = Array.Empty<string>(),
            });

            var xmlPath = Path.Combine(AppContext.BaseDirectory, "WebScraper.Api.xml");
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }

    private static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Reuse the same provider/connection as the domain DbContext so Identity tables
        // live alongside (with a different __EFMigrationsHistory table) — see AuthDbContext.
        var provider = configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AuthDbContext>(options =>
        {
            switch (provider.ToLowerInvariant())
            {
                case "sqlite":
                    options.UseSqlite(ResolveSqlitePath(connectionString),
                        sql => sql.MigrationsHistoryTable(AuthDbContext.MigrationsHistoryTable));
                    break;
                case "postgresql":
                    options.UseNpgsql(connectionString,
                        pg => pg.MigrationsHistoryTable(AuthDbContext.MigrationsHistoryTable));
                    break;
                case "sqlserver":
                    options.UseSqlServer(connectionString,
                        ms => ms.MigrationsHistoryTable(AuthDbContext.MigrationsHistoryTable));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported database provider: {provider}");
            }
        });

        services.AddIdentityCore<AppUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
        })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.Configure<InitialAdminSettings>(configuration.GetSection(InitialAdminSettings.SectionName));
        services.AddScoped<IdentitySeeder>();

        return services;
    }

    private static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bootstrap API keys (file-based, optional)
        services.Configure<ApiKeyOptions>(configuration.GetSection(ApiKeyOptions.SectionName));

        // JWT settings — fail fast if the signing key is missing in non-dev
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
        services.AddScoped<JwtTokenService>();

        services.AddAuthentication(options =>
            {
                // A "smart" policy scheme is the default: per request it forwards to the
                // correct concrete scheme based on the path (see ForwardDefaultSelector below).
                // This keeps HttpContext.User populated correctly for Blazor SSR + the
                // cascading auth state, and makes a bare [Authorize] on a dashboard page
                // challenge via the cookie scheme (302 to /admin/login) instead of the API
                // key scheme (401 application/problem+json). The concrete schemes
                // (ApiKey, JWT, AdminCookie) are still pinned explicitly by each policy.
                options.DefaultScheme = SmartSchemeName;
                options.DefaultChallengeScheme = SmartSchemeName;
            })
            .AddPolicyScheme(SmartSchemeName, SmartSchemeName, options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var path = context.Request.Path;

                    // Admin dashboard SSR pages + the Blazor interactive circuit ride the
                    // cookie issued at /admin/login.
                    if (path.StartsWithSegments("/admin") || path.StartsWithSegments("/_blazor"))
                        return AuthorizationPolicies.CookieSchemeName;

                    // The real-time scrape hub authenticates with a JWT (?access_token=…).
                    if (path.StartsWithSegments("/hubs"))
                        return JwtBearerDefaults.AuthenticationScheme;

                    // Everything else (the REST API) uses the X-Api-Key scheme.
                    return ApiKeyAuthenticationOptions.SchemeName;
                };
            })
            .AddCookie(AuthorizationPolicies.CookieSchemeName, options =>
            {
                options.LoginPath = "/admin/login";
                options.AccessDeniedPath = "/admin/login";
                options.Cookie.Name = "WebScraper.Admin";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationOptions.SchemeName, _ => { })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.RequireHttpsMetadata = false; // localhost dev; production should be true (behind a reverse proxy)
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = string.IsNullOrWhiteSpace(jwtSettings.SigningKey)
                        ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes("dev-placeholder-key-replace-in-config-32-chars!"))
                        : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                };

                // SignalR WebSocket connections can't set Authorization headers from the
                // browser — accept the JWT via the standard ?access_token=… query string
                // for any request targeting /hubs/*.
                options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    // Copy of the helper in Core's ServiceCollectionExtensions — kept private here so the
    // Identity DbContext gets the same resolved-path treatment without spelunking through Core.
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
