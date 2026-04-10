using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using WebScraper.Api.Extensions;
using WebScraper.Api.Middleware;
using WebScraper.Data;
using WebScraper.Extensions;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog (read from appsettings) ---
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// --- Load Local (secrets) overrides if present ---
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// --- Core services: DbContext, repositories, scrapers (shared with CLI) ---
builder.Services.AddWebScraperServices(builder.Configuration);

// --- API host specific: auth, query log writer, Swagger ---
builder.Services.AddWebScraperApiServices(builder.Configuration);

// --- Controllers + RFC 7807 Problem Details ---
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Standard ASP.NET Core already returns ProblemDetails from [ApiController] model-state
        // errors — this just tweaks the type URI so clients know where to look.
        options.ClientErrorMapping[StatusCodes.Status404NotFound].Link =
            "https://tools.ietf.org/html/rfc7231#section-6.5.4";
    });
builder.Services.AddProblemDetails();

// --- Health checks ---
var healthChecksBuilder = builder.Services.AddHealthChecks();
var dbProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    if (dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        healthChecksBuilder.AddNpgSql(connectionString, name: "postgres", tags: new[] { "db", "ready" });
    }
    else if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        healthChecksBuilder.AddSqlite(connectionString, name: "sqlite", tags: new[] { "db", "ready" });
    }
}

var app = builder.Build();

// --- Apply pending migrations on startup (matches CLI behavior) ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// --- Middleware pipeline ---
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "WebScraper API v1");
        options.RoutePrefix = "swagger";
    });
}

// Problem Details for unhandled exceptions → RFC 7807 JSON.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

// Query logging sits AFTER auth so we can stamp the ApiKeyId claim on log rows.
app.UseMiddleware<ApiQueryLoggingMiddleware>();

app.MapControllers();

// Liveness: process is up.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});

// Readiness: DB reachable too.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

// Back-compat default.
app.MapHealthChecks("/health");

try
{
    Log.Information("Starting WebScraper.Api host");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "WebScraper.Api host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

// Exposed so integration tests (M5) can use WebApplicationFactory<Program>.
public partial class Program { }
