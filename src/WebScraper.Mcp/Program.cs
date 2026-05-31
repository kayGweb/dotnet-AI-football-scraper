using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using WebScraper.Mcp;
using WebScraper.Mcp.Configuration;

var builder = Host.CreateApplicationBuilder(args);

// ---------------------------------------------------------------------------
// Logging — CRITICAL: route everything to stderr.
// Stdio MCP transport reserves stdout for protocol frames; any stdout write
// from us would corrupt the framing and break the client.
// ---------------------------------------------------------------------------
builder.Logging.ClearProviders();
builder.Logging.AddConsole(opts =>
{
    opts.LogToStandardErrorThreshold = LogLevel.Trace;
});

// ---------------------------------------------------------------------------
// Settings — environment variables win over appsettings so the same binary can
// be reused for local dev and prod. Claude Desktop / Claude Code passes them
// in the "env" block of the MCP server config.
// ---------------------------------------------------------------------------
builder.Configuration.AddEnvironmentVariables();

var settings = new McpSettings();
builder.Configuration.GetSection(McpSettings.SectionName).Bind(settings);

// Direct env overrides (NFL_API_URL / NFL_API_KEY are the user-facing knobs).
var envUrl = Environment.GetEnvironmentVariable("NFL_API_URL");
var envKey = Environment.GetEnvironmentVariable("NFL_API_KEY");
if (!string.IsNullOrWhiteSpace(envUrl)) settings.ApiBaseUrl = envUrl;
if (!string.IsNullOrWhiteSpace(envKey)) settings.ApiKey = envKey;

builder.Services.AddSingleton(settings);

// ---------------------------------------------------------------------------
// Typed HttpClient for talking to the NFL Web API.
// ---------------------------------------------------------------------------
builder.Services.AddHttpClient<NflApiClient>(http =>
{
    http.BaseAddress = new Uri(settings.ApiBaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
    http.DefaultRequestHeaders.Add("X-Api-Key", settings.ApiKey);
    http.DefaultRequestHeaders.Add("User-Agent", "WebScraper.Mcp/1.0");
    http.DefaultRequestHeaders.Add("Accept", "application/json");
});

// ---------------------------------------------------------------------------
// MCP server: stdio transport + auto-discover tools from this assembly.
// ---------------------------------------------------------------------------
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var host = builder.Build();

// Sanity check at startup — log to stderr so the user sees it in Claude Code's
// MCP debug pane / Claude Desktop's logs, but don't block on missing config:
// users may legitimately want to run the server and only call read tools that
// hit a public, no-auth API instance during dev.
var startupLogger = host.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("WebScraper.Mcp starting. ApiBaseUrl={Url}, ApiKeyConfigured={HasKey}",
    settings.ApiBaseUrl,
    !string.IsNullOrWhiteSpace(settings.ApiKey));
if (string.IsNullOrWhiteSpace(settings.ApiKey))
{
    startupLogger.LogWarning(
        "NFL_API_KEY is not set — requests will fail with 401 unless the API is " +
        "running without authentication. Set NFL_API_KEY in the MCP server's env block.");
}

await host.RunAsync();
