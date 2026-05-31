namespace WebScraper.Mcp.Configuration;

/// <summary>
/// Configuration for the MCP server. Driven entirely by environment variables so
/// the same binary can be reused for local dev (talks to localhost:5080) and
/// production (talks to the deployed API). The client (Claude Desktop / Claude
/// Code) passes the values via the <c>env</c> block in its MCP server config.
/// </summary>
public class McpSettings
{
    public const string SectionName = "Mcp";

    /// <summary>Base URL of the NFL Web API (no trailing slash required).</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5080";

    /// <summary>API key to pass via the <c>X-Api-Key</c> header.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Per-request timeout for upstream API calls.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
