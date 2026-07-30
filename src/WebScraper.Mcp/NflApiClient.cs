using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WebScraper.Mcp;

/// <summary>
/// Thin HTTP wrapper over the WebScraper.Api endpoints. Tools return the raw JSON
/// body so Claude sees the full structured response (and can reason about meta /
/// pagination fields). On error we return a small JSON envelope rather than
/// throwing — that way Claude gets a useful tool result instead of an MCP
/// protocol-level failure.
/// </summary>
public class NflApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<NflApiClient> _logger;

    public NflApiClient(HttpClient http, ILogger<NflApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    // ---- Teams ----

    public Task<string> ListTeamsAsync(string? conference, int page, int pageSize, CancellationToken ct)
        => GetAsync(BuildQuery("api/v1/teams",
            ("conference", conference),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString())), ct);

    public Task<string> GetTeamByIdAsync(int id, CancellationToken ct)
        => GetAsync($"api/v1/teams/{id}", ct);

    public Task<string> GetTeamByAbbreviationAsync(string abbreviation, CancellationToken ct)
        => GetAsync($"api/v1/teams/by-abbreviation/{Uri.EscapeDataString(abbreviation)}", ct);

    // ---- Players ----

    public Task<string> ListPlayersAsync(
        int? teamId, string? teamAbbreviation, string? position,
        int page, int pageSize, CancellationToken ct)
        => GetAsync(BuildQuery("api/v1/players",
            ("teamId", teamId?.ToString()),
            ("teamAbbreviation", teamAbbreviation),
            ("position", position),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString())), ct);

    public Task<string> GetPlayerByIdAsync(int id, CancellationToken ct)
        => GetAsync($"api/v1/players/{id}", ct);

    public Task<string> GetPlayerStatsAsync(int id, int? season, int? week, CancellationToken ct)
        => GetAsync(BuildQuery($"api/v1/players/{id}/stats",
            ("season", season?.ToString()),
            ("week", week?.ToString())), ct);

    // ---- Games ----

    public Task<string> ListGamesAsync(
        int? season, int? week, int? teamId,
        int page, int pageSize, CancellationToken ct)
        => GetAsync(BuildQuery("api/v1/games",
            ("season", season?.ToString()),
            ("week", week?.ToString()),
            ("teamId", teamId?.ToString()),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString())), ct);

    public Task<string> GetGameByIdAsync(int id, CancellationToken ct)
        => GetAsync($"api/v1/games/{id}", ct);

    public Task<string> GetGameTeamStatsAsync(int id, CancellationToken ct)
        => GetAsync($"api/v1/games/{id}/team-stats", ct);

    public Task<string> GetGamePlayerStatsAsync(int id, CancellationToken ct)
        => GetAsync($"api/v1/games/{id}/player-stats", ct);

    public Task<string> GetGameInjuriesAsync(int id, CancellationToken ct)
        => GetAsync($"api/v1/games/{id}/injuries", ct);

    public Task<string> GetGameDrivesAsync(int id, CancellationToken ct)
        => GetAsync($"api/v1/games/{id}/drives", ct);

    public Task<string> GetGameScoringPlaysAsync(int id, CancellationToken ct)
        => GetAsync($"api/v1/games/{id}/scoring-plays", ct);

    public Task<string> GetGameWeatherAsync(int id, CancellationToken ct)
        => GetAsync($"api/v1/games/{id}/weather", ct);

    public Task<string> GetGameOfficialsAsync(int id, CancellationToken ct)
        => GetAsync($"api/v1/games/{id}/officials", ct);

    public Task<string> GetGameOddsAsync(int id, CancellationToken ct)
        => GetAsync($"api/v1/games/{id}/odds", ct);

    // ---- Venues ----

    public Task<string> ListVenuesAsync(string? state, bool? isIndoor, int page, int pageSize, CancellationToken ct)
        => GetAsync(BuildQuery("api/v1/venues",
            ("state", state),
            ("isIndoor", isIndoor?.ToString().ToLowerInvariant()),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString())), ct);

    public Task<string> GetVenueByIdAsync(int id, CancellationToken ct)
        => GetAsync($"api/v1/venues/{id}", ct);

    // ---- Status ----

    public Task<string> GetStatusAsync(CancellationToken ct)
        => GetAsync("api/v1/status", ct);

    // ---- Operate ----

    public Task<string> TriggerScrapeAsync(string type, int? season, int? week, string? seasonType, int? endSeason, CancellationToken ct)
    {
        var isBackfill = type.Equals("backfill", StringComparison.OrdinalIgnoreCase);
        var body = JsonSerializer.Serialize(new
        {
            season,
            week = isBackfill ? null : week,
            seasonType,
            endSeason = isBackfill ? (endSeason ?? week ?? season) : null,
        });
        return PostAsync($"api/v1/scrape/{type.ToLowerInvariant()}", body, ct);
    }

    public Task<string> ListJobsAsync(string? status, int page, int pageSize, CancellationToken ct)
        => GetAsync(BuildQuery("api/v1/jobs",
            ("status", status),
            ("page", page.ToString()),
            ("pageSize", pageSize.ToString())), ct);

    public Task<string> GetJobAsync(int jobId, CancellationToken ct)
        => GetAsync($"api/v1/jobs/{jobId}", ct);

    public Task<string> RetryJobAsync(int jobId, CancellationToken ct)
        => PostAsync($"api/v1/jobs/{jobId}/retry", "{}", ct);

    public Task<string> EstimateBackfillAsync(int startSeason, int endSeason, CancellationToken ct)
        => GetAsync(BuildQuery("api/v1/backfill/estimate",
            ("startSeason", startSeason.ToString()),
            ("endSeason", endSeason.ToString())), ct);

    public Task<string> StartBackfillAsync(int startSeason, int endSeason, bool backupFirst, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new { startSeason, endSeason, backupFirst });
        return PostAsync("api/v1/backfill", body, ct);
    }

    public Task<string> GetBackfillProgressAsync(int jobId, CancellationToken ct)
        => GetAsync($"api/v1/backfill/{jobId}/progress", ct);

    public Task<string> PauseBackfillAsync(int jobId, CancellationToken ct)
        => PostAsync($"api/v1/backfill/{jobId}/pause", "{}", ct);

    public Task<string> ResumeBackfillAsync(int jobId, CancellationToken ct)
        => PostAsync($"api/v1/backfill/{jobId}/resume", "{}", ct);

    public Task<string> GetPushStatusAsync(CancellationToken ct)
        => GetAsync("api/v1/push/status", ct);

    public Task<string> TriggerPushAsync(bool resume, bool reset, CancellationToken ct)
        => PostAsync($"api/v1/push?resume={resume.ToString().ToLowerInvariant()}&reset={reset.ToString().ToLowerInvariant()}", "{}", ct);

    public Task<string> CreateBackupAsync(CancellationToken ct)
        => PostAsync("api/v1/backup", "{}", ct);

    public Task<string> ListBackupsAsync(CancellationToken ct)
        => GetAsync("api/v1/backup", ct);

    public Task<string> GetCoverageAsync(int? season, string? seasonType, CancellationToken ct)
        => GetAsync(BuildQuery("api/v1/coverage",
            ("season", season?.ToString()),
            ("seasonType", seasonType)), ct);

    public Task<string> FindGapsAsync(int limit, CancellationToken ct)
        => GetAsync(BuildQuery("api/v1/gaps", ("limit", limit.ToString())), ct);

    public Task<string> RefreshCoverageAsync(int? season, string? seasonType, bool enqueueRepairs, CancellationToken ct)
        => PostAsync(BuildQuery("api/v1/coverage/refresh",
            ("season", season?.ToString()),
            ("seasonType", seasonType),
            ("enqueueRepairs", enqueueRepairs.ToString().ToLowerInvariant())), "{}", ct);

    // ---- Quality ----

    public Task<string> GetQualityFindingsAsync(int limit, CancellationToken ct)
        => GetAsync(BuildQuery("api/v1/quality/findings", ("limit", limit.ToString())), ct);

    public Task<string> ScanQualityAsync(int? season, string? seasonType, int? week, CancellationToken ct)
        => PostAsync(BuildQuery("api/v1/quality/scan",
            ("season", season?.ToString()),
            ("seasonType", seasonType),
            ("week", week?.ToString())), "{}", ct);

    public Task<string> RepairFindingAsync(long findingId, CancellationToken ct)
        => PostAsync($"api/v1/quality/findings/{findingId}/repair", "{}", ct);

    public Task<string> EnqueueRepairsAsync(int limit, CancellationToken ct)
        => PostAsync(BuildQuery("api/v1/quality/repairs", ("limit", limit.ToString())), "{}", ct);

    // ---- Introspect ----

    public Task<string> DescribeSchemaAsync(string? entity, CancellationToken ct)
        => GetAsync(BuildQuery("api/v1/schema", ("entity", entity)), ct);

    public Task<string> GetDataDictionaryAsync(CancellationToken ct)
        => GetAsync("api/v1/schema/dictionary", ct);

    public Task<string> QueryStatsAsync(string requestJson, CancellationToken ct)
        => PostAsync("api/v1/query/stats", requestJson, ct);

    // ---- Propose ----

    public Task<string> ProposeCorrectionAsync(string bodyJson, CancellationToken ct)
        => PostAsync("api/v1/corrections", bodyJson, ct);

    public Task<string> ListCorrectionsAsync(string? status, CancellationToken ct)
        => GetAsync(BuildQuery("api/v1/corrections", ("status", status)), ct);

    // ---- Internal helpers ----

    private async Task<string> PostAsync(string path, string jsonBody, CancellationToken ct)
    {
        try
        {
            using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(path, content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
                return body;

            return ErrorEnvelope((int)response.StatusCode, ReasonFor(response.StatusCode), path, body);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "NFL API POST timed out: {Path}", path);
            return ErrorEnvelope(0, "Request timed out", path, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "NFL API POST failed: {Path}", path);
            return ErrorEnvelope(0, "Network error reaching NFL API", path, ex.Message);
        }
    }

    private async Task<string> GetAsync(string path, CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(path, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return body;
            }

            // Surface auth + not-found cleanly so Claude gets actionable feedback.
            return ErrorEnvelope((int)response.StatusCode, ReasonFor(response.StatusCode), path, body);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "NFL API request timed out: {Path}", path);
            return ErrorEnvelope(0, "Request timed out", path, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "NFL API request failed: {Path}", path);
            return ErrorEnvelope(0, "Network error reaching NFL API", path, ex.Message);
        }
    }

    private static string ReasonFor(HttpStatusCode code) => code switch
    {
        HttpStatusCode.Unauthorized => "API key missing or invalid (set NFL_API_KEY).",
        HttpStatusCode.Forbidden => "API key lacks the required scope.",
        HttpStatusCode.NotFound => "Resource not found.",
        HttpStatusCode.TooManyRequests => "Rate limited by the API — back off and retry.",
        _ => $"NFL API returned HTTP {(int)code}.",
    };

    private static string ErrorEnvelope(int status, string reason, string path, string? detail)
    {
        // Minimal JSON envelope — Claude will see this as the tool result and
        // can decide whether to retry, ask the user, or surface the error.
        var sb = new StringBuilder();
        sb.Append('{')
          .Append("\"error\":true,")
          .Append("\"status\":").Append(status).Append(',')
          .Append("\"reason\":").Append(JsonString(reason)).Append(',')
          .Append("\"path\":").Append(JsonString(path));
        if (!string.IsNullOrWhiteSpace(detail))
        {
            sb.Append(",\"detail\":").Append(JsonString(detail));
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static string JsonString(string s)
    {
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append($"\\u{(int)c:x4}");
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static string BuildQuery(string path, params (string key, string? value)[] parts)
    {
        var entries = parts
            .Where(p => !string.IsNullOrWhiteSpace(p.value))
            .Select(p => $"{p.key}={Uri.EscapeDataString(p.value!)}")
            .ToList();
        return entries.Count == 0 ? path : $"{path}?{string.Join("&", entries)}";
    }
}
