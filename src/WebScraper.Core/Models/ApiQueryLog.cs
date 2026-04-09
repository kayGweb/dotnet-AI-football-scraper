namespace WebScraper.Models;

/// <summary>
/// Observability record for every request hitting the public API. Captured via middleware
/// after auth runs, so we know which API key consumer made the call. Written asynchronously
/// through a Channel-backed background writer — never blocks the hot path.
/// Used by the Blazor admin dashboard at /admin/api-usage to visualize LLM/consumer traffic.
/// </summary>
public class ApiQueryLog
{
    public long Id { get; set; }

    /// <summary>UTC timestamp when the request was received.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Opaque identifier of the API key used for this request. Null for unauthenticated requests.</summary>
    public string? ApiKeyId { get; set; }

    /// <summary>Friendly name of the API key (e.g. "Claude MCP (primary)"). Null if unauthenticated.</summary>
    public string? ApiKeyName { get; set; }

    /// <summary>HTTP method (GET, POST, etc.).</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>Request path without query string (e.g. "/api/v1/players/123").</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Full query string including the leading '?' if present.</summary>
    public string? QueryString { get; set; }

    /// <summary>HTTP response status code.</summary>
    public int StatusCode { get; set; }

    /// <summary>Request duration in milliseconds (server-side, excludes network time).</summary>
    public int DurationMs { get; set; }

    /// <summary>Size of the response body in bytes, if measurable.</summary>
    public int ResponseBytes { get; set; }

    /// <summary>Client user-agent header.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Correlation id stamped on the response via middleware and logged in Serilog.</summary>
    public string? CorrelationId { get; set; }
}
