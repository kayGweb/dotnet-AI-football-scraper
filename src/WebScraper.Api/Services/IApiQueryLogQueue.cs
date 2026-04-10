using WebScraper.Models;

namespace WebScraper.Api.Services;

/// <summary>
/// Write-only facade over the background <see cref="ApiQueryLogQueue"/> so middleware
/// never needs to know about the underlying Channel. The queue is lossy on overflow
/// — we log a warning and drop rather than blocking the hot path.
/// </summary>
public interface IApiQueryLogQueue
{
    /// <summary>Attempts to enqueue the log for async persistence. Returns false if the channel is full.</summary>
    bool TryEnqueue(ApiQueryLog entry);
}
