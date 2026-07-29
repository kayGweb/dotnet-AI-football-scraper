using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Api.Services;

/// <summary>
/// Periodically enqueues <see cref="ScrapeJobType.OddsPoll"/> jobs so opening/closing
/// lines are captured before kickoff (§5.1).
/// </summary>
public class OddsPollScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobQueue _queue;
    private readonly OddsPollSettings _settings;
    private readonly ILogger<OddsPollScheduler> _logger;

    public OddsPollScheduler(
        IServiceScopeFactory scopeFactory,
        IJobQueue queue,
        IOptions<OddsPollSettings> settings,
        ILogger<OddsPollScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("OddsPoll scheduler is disabled");
            return;
        }

        // Stagger first run so startup migrations/seeding finish first.
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnqueuePollJobIfDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OddsPoll scheduler failed to enqueue job");
            }

            await Task.Delay(TimeSpan.FromHours(_settings.IntervalHours), stoppingToken);
        }
    }

    private async Task EnqueuePollJobIfDueAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow.AddHours(-_settings.IntervalHours);
        var recent = await db.ScrapeJobs
            .AsNoTracking()
            .Where(j => j.Type == ScrapeJobType.OddsPoll)
            .Where(j => j.Status == ScrapeJobStatus.Queued || j.Status == ScrapeJobStatus.Running
                || (j.Status == ScrapeJobStatus.Succeeded && j.CompletedAt >= cutoff))
            .AnyAsync(cancellationToken);

        if (recent)
        {
            _logger.LogDebug("Skipping OddsPoll enqueue — recent job already queued, running, or succeeded");
            return;
        }

        var job = new ScrapeJob
        {
            Type = ScrapeJobType.OddsPoll,
            Source = "scheduler",
            Status = ScrapeJobStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            RequestedBy = "odds-poll-scheduler",
        };

        db.ScrapeJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        db.ScrapeEvents.Add(new ScrapeEvent
        {
            JobId = job.Id,
            EventType = ScrapeEventType.JobQueued,
            Timestamp = DateTime.UtcNow,
            Payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                type = job.Type.ToString(),
                source = job.Source,
                requestedBy = job.RequestedBy,
            }),
        });

        await db.SaveChangesAsync(cancellationToken);
        _queue.TryEnqueue(job.Id);

        _logger.LogInformation("Enqueued scheduled OddsPoll job {JobId}", job.Id);
    }
}
