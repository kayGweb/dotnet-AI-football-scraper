using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;
using WebScraper.Api.Auth;
using WebScraper.Api.Services;

namespace WebScraper.Api.Extensions;

/// <summary>
/// Wires up API-host-specific services (auth, query logging, Swagger). Core
/// services (DbContext, repositories, scrapers) come from
/// <see cref="WebScraper.Extensions.ServiceCollectionExtensions.AddWebScraperServices"/>.
/// </summary>
public static class ApiServiceCollectionExtensions
{
    public static IServiceCollection AddWebScraperApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- API key auth ---
        services.Configure<ApiKeyOptions>(configuration.GetSection(ApiKeyOptions.SectionName));

        services.AddAuthentication(ApiKeyAuthenticationOptions.SchemeName)
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationOptions.SchemeName,
                _ => { });

        services.AddAuthorization(options => options.AddWebScraperApiAuthorization());

        // --- Query log queue + background writer ---
        services.AddSingleton<ApiQueryLogQueue>();
        services.AddSingleton<IApiQueryLogQueue>(sp => sp.GetRequiredService<ApiQueryLogQueue>());
        services.AddHostedService<ApiQueryLogWriter>();

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
                    "Read-only REST API over the NFL data store. All endpoints require an API key " +
                    "passed via the X-Api-Key header. Pagination defaults to page=1, pageSize=25 " +
                    "(max 200). Responses include the X-Total-Count header for list endpoints.",
            });

            options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Name = ApiKeyAuthenticationHandler.HeaderName,
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "API key issued via the admin dashboard (SHA-256 hashed at rest).",
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "ApiKey",
                    },
                }] = Array.Empty<string>(),
            });

            // Include XML comments so Swagger UI shows the /// <summary> blocks.
            var xmlPath = Path.Combine(AppContext.BaseDirectory, "WebScraper.Api.xml");
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }
}
