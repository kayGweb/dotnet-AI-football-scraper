using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Api.Services;

/// <summary>
/// BackgroundService that drains <see cref="ApiQueryLogQueue"/> and batch-inserts
/// entries into <see cref="AppDbContext.ApiQueryLogs"/>. Batches flush either when
/// <see cref="BatchSize"/> is reached or when <see cref="FlushInterval"/> elapses,
/// whichever comes first — keeping DB round-trips low without letting logs sit in
/// memory indefinitely during low-traffic periods.
/// </summary>
public class ApiQueryLogWriter : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);

    private readonly ApiQueryLogQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ApiQueryLogWriter> _logger;

    public ApiQueryLogWriter(
        ApiQueryLogQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ApiQueryLogWriter> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var buffer = new List<ApiQueryLog>(BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for at least one entry, then drain up to BatchSize with a
                // short timeout so steady streams still batch.
                if (!await _queue.Reader.WaitToReadAsync(stoppingToken))
                    break;

                using var flushTimeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                flushTimeout.CancelAfter(FlushInterval);

                while (buffer.Count < BatchSize && _queue.Reader.TryRead(out var entry))
                {
                    buffer.Add(entry);
                }

                if (buffer.Count == 0)
                    continue;

                await FlushAsync(buffer, stoppingToken);
                buffer.Clear();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist {Count} ApiQueryLog entries — dropping batch.", buffer.Count);
                buffer.Clear();
            }
        }

        // Drain anything left in the channel on shutdown.
        if (buffer.Count > 0)
        {
            try { await FlushAsync(buffer, CancellationToken.None); }
            catch (Exception ex) { _logger.LogError(ex, "Final ApiQueryLog flush failed."); }
        }
    }

    private async Task FlushAsync(List<ApiQueryLog> batch, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ApiQueryLogs.AddRange(batch);
        await db.SaveChangesAsync(cancellationToken);
    }
}
