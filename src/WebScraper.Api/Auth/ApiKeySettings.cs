namespace WebScraper.Api.Auth;

/// <summary>
/// Binds the "ApiKeys" section from appsettings.json. Each entry is a statically
/// configured API key for a consumer (e.g. the MCP server, a CI job, a Claude skill).
///
/// MVP design: keys are stored as SHA-256 hex digests so that a leaked appsettings
/// file does not expose the plaintext secret. A future milestone will move key
/// management into the database behind an admin UI.
/// </summary>
public class ApiKeyOptions
{
    public const string SectionName = "ApiKeys";

    public List<ApiKeyEntry> Keys { get; set; } = new();
}

public class ApiKeyEntry
{
    /// <summary>Opaque identifier for the key. Surfaced in <see cref="WebScraper.Models.ApiQueryLog.ApiKeyId"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-friendly name for dashboards (e.g. "Claude MCP (primary)").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>SHA-256 hex digest of the plaintext key. Lowercase, no separators.</summary>
    public string HashedKey { get; set; } = string.Empty;

    /// <summary>Scopes granted to this key. M1 recognises "read"; future milestones add "write", "realtime".</summary>
    public List<string> Scopes { get; set; } = new();
}
