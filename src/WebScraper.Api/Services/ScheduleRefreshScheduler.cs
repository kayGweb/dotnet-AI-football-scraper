using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebScraper.Data;
using WebScraper.Models;
using WebScraper.Services.Coverage;

namespace WebScraper.Api.Services;

/// <summary>
/// Keeps the current season's schedule loaded and fresh by periodically enqueueing a
/// full-season <see cref="ScrapeJobType.Games"/> job. ESPN's scoreboard returns
/// scheduled games as well as finals, so one games scrape loads every week of the
/// schedule and later runs pick up final scores, flexed kickoff times, and postponements.
/// </summary>
public class ScheduleRefreshScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobQueue _queue;
    private readonly ScheduleRefreshSettings _settings;
    private readonly ScraperSettings _scraperSettings;
    private readonly ILogger<ScheduleRefreshScheduler> _logger;

    public ScheduleRefreshScheduler(
        IServiceScopeFactory scopeFactory,
        IJobQueue queue,
        IOptions<ScheduleRefreshSettings> settings,
        IOptions<ScraperSettings> scraperSettings,
        ILogger<ScheduleRefreshScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _settings = settings.Value;
        _scraperSettings = scraperSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Schedule refresh scheduler is disabled");
            return;
        }

        // Stagger first run so startup migrations/seeding finish first.
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var (season, seasonType) in GetSeasonTypesToRefresh(DateTime.UtcNow))
                    await EnqueueIfDueAsync(season, seasonType, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Schedule refresh scheduler failed to enqueue job");
            }

            await Task.Delay(TimeSpan.FromHours(Math.Max(1, _settings.IntervalHours)), stoppingToken);
        }
    }

    /// <summary>
    /// Regular season is always refreshed for the current season; postseason is added
    /// from January onward, once playoff matchups start to exist on ESPN.
    /// </summary>
    internal static IReadOnlyList<(int Season, NflSeasonType SeasonType)> GetSeasonTypesToRefresh(DateTime utcNow)
    {
        var season = NflSeasonSchedule.GetCurrentSeason(utcNow);
        var result = new List<(int, NflSeasonType)> { (season, NflSeasonType.Regular) };
        if (utcNow.Month <= 2)
            result.Add((season, NflSeasonType.Postseason));
        return result;
    }

    private async Task EnqueueIfDueAsync(int season, NflSeasonType seasonType, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow.AddHours(-Math.Max(1, _settings.IntervalHours));

        // Week == null means a full-season games job (manual or scheduled) — either
        // one already covers what this refresh would do.
        var recent = await db.ScrapeJobs
            .AsNoTracking()
            .Where(j => j.Type == ScrapeJobType.Games && j.Season == season && j.Week == null)
            .Where(j => j.SeasonType == seasonType
                || (seasonType == NflSeasonType.Regular && j.SeasonType == null))
            .Where(j => j.Status == ScrapeJobStatus.Queued || j.Status == ScrapeJobStatus.Running
                || (j.Status == ScrapeJobStatus.Succeeded && j.CompletedAt >= cutoff))
            .AnyAsync(cancellationToken);

        if (recent)
        {
            _logger.LogDebug(
                "Skipping schedule refresh for {Season} {SeasonType} — recent games job already queued, running, or succeeded",
                season, seasonType);
            return;
        }

        var job = new ScrapeJob
        {
            Type = ScrapeJobType.Games,
            Source = _scraperSettings.DataProvider,
            Season = season,
            SeasonType = seasonType,
            Status = ScrapeJobStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            RequestedBy = "schedule-refresh-scheduler",
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
                season = job.Season,
                seasonType = job.SeasonType?.ToString(),
                requestedBy = job.RequestedBy,
            }),
        });

        await db.SaveChangesAsync(cancellationToken);
        _queue.TryEnqueue(job.Id);

        _logger.LogInformation(
            "Enqueued scheduled games job {JobId} for {Season} {SeasonType}", job.Id, season, seasonType);
    }
}
