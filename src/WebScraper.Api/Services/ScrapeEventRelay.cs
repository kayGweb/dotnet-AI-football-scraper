using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebScraper.Api.Dtos.Admin;
using WebScraper.Api.Hubs;
using WebScraper.Data;

namespace WebScraper.Api.Services;

/// <summary>
/// Polls the ScrapeEvents outbox in ID order and broadcasts new events to
/// SignalR subscribers via <see cref="ScraperHub"/>. Single-instance: the
/// last-seen cursor lives in memory, so scaling out requires either disabling
/// this service on replicas or moving the cursor to a shared store.
/// </summary>
public class ScrapeEventRelay : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private const int BatchSize = 200;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<ScraperHub> _hub;
    private readonly ILogger<ScrapeEventRelay> _logger;

    public ScrapeEventRelay(
        IServiceScopeFactory scopeFactory,
        IHubContext<ScraperHub> hub,
        ILogger<ScrapeEventRelay> logger)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start from the latest existing event so we don't replay history on every restart.
        // New connections that need history call GET /api/v1/events?since= explicitly.
        long lastSeenId = await GetInitialCursorAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                lastSeenId = await PollAndBroadcastAsync(lastSeenId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScrapeEventRelay poll failed — will retry");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<long> GetInitialCursorAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ScrapeEvents.MaxAsync(e => (long?)e.Id, ct) ?? 0L;
    }

    private async Task<long> PollAndBroadcastAsync(long lastSeenId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var events = await db.ScrapeEvents
            .AsNoTracking()
            .Where(e => e.Id > lastSeenId)
            .OrderBy(e => e.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (events.Count == 0)
            return lastSeenId;

        foreach (var evt in events)
        {
            await _hub.Clients.All.SendAsync("ScrapeEvent", evt.ToDto(), ct);
        }

        return events[^1].Id;
    }
}
