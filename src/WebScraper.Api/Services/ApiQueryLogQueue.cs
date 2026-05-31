using System.Threading.Channels;
using WebScraper.Models;

namespace WebScraper.Api.Services;

/// <summary>
/// Bounded in-memory channel that decouples request-handling middleware from
/// the database writer. On overflow we drop the oldest entries so the hot path
/// never waits on the DB. The consumer is <see cref="ApiQueryLogWriter"/>.
/// </summary>
public class ApiQueryLogQueue : IApiQueryLogQueue
{
    // 10k entries at ~400 bytes each ≈ 4 MB upper bound — more than enough to
    // absorb a DB outage without blowing up memory.
    private const int Capacity = 10_000;

    private readonly Channel<ApiQueryLog> _channel;
    private readonly ILogger<ApiQueryLogQueue> _logger;
    private long _droppedCount;

    public ApiQueryLogQueue(ILogger<ApiQueryLogQueue> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<ApiQueryLog>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public ChannelReader<ApiQueryLog> Reader => _channel.Reader;

    public bool TryEnqueue(ApiQueryLog entry)
    {
        if (_channel.Writer.TryWrite(entry))
        {
            return true;
        }

        var dropped = Interlocked.Increment(ref _droppedCount);
        if (dropped % 100 == 1)
        {
            _logger.LogWarning(
                "ApiQueryLog channel full — dropped {Count} total entries so far.", dropped);
        }
        return false;
    }
}
