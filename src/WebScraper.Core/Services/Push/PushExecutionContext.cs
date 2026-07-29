using WebScraper.Data;
using WebScraper.Models;
using WebScraper.Services.Push;

namespace WebScraper.Services;

internal sealed class PushExecutionContext
{
    public PushExecutionContext(
        AppDbContext localDb,
        AppDbContext remoteDb,
        ConsoleDisplayService display,
        DatabasePushSession session,
        int batchSize)
    {
        LocalDb = localDb;
        RemoteDb = remoteDb;
        Display = display;
        Session = session;
        BatchSize = batchSize;
    }

    public AppDbContext LocalDb { get; }
    public AppDbContext RemoteDb { get; }
    public ConsoleDisplayService Display { get; }
    public DatabasePushSession Session { get; }
    public int BatchSize { get; }
    public DatabasePushIdMaps Maps { get; set; } = new();
    public List<string> Errors { get; } = new();
}

internal static class PushTime
{
    public static DateTime ToUtc(DateTime dt) =>
        dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Utc) : dt.ToUniversalTime();

    public static DateTime? ToUtcOrNull(DateTime? dt) =>
        dt.HasValue ? ToUtc(dt.Value) : null;
}
