using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using WebScraper.Data;
using WebScraper.Models;
using WebScraper.Services.Push;

namespace WebScraper.Services;

public class DatabasePushService
{
    private static readonly ILogger Logger = Log.ForContext<DatabasePushService>();
    private readonly PushSettings _settings;

    public DatabasePushService(IOptions<PushSettings>? settings = null)
    {
        _settings = settings?.Value ?? new PushSettings();
    }

    /// <summary>
    /// Pushes data from local SQLite to remote PostgreSQL in batched, resumable stages.
    /// Progress is checkpointed to <see cref="DatabasePushSession"/> in the local DB.
    /// </summary>
    public Task<ScrapeResult> PushToServerAsync(
        AppDbContext localDb,
        string postgresConnectionString,
        ConsoleDisplayService display,
        PushOptions? options = null,
        CancellationToken cancellationToken = default)
        => PushToServerAsync(localDb, postgresConnectionString, display, options, null, cancellationToken);

    public async Task<ScrapeResult> PushToServerAsync(
        AppDbContext localDb,
        string postgresConnectionString,
        ConsoleDisplayService display,
        PushOptions? options,
        int? batchSizeOverride,
        CancellationToken cancellationToken = default)
    {
        options ??= PushOptions.Default;
        var batchSize = batchSizeOverride ?? options.BatchSize ?? _settings.BatchSize;

        DatabasePushSession session;
        try
        {
            session = await DatabasePushSessionStore.PrepareAsync(localDb, options, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return ScrapeResult.Failed(ex.Message);
        }

        var pgOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgresConnectionString)
            .Options;

        await using var remoteDb = new AppDbContext(pgOptions);
        var ctx = new PushExecutionContext(localDb, remoteDb, display, session, batchSize);

        try
        {
            display.PrintInfo(options.Resume
                ? $"Resuming push from stage {session.CurrentStage} (offset {session.StageOffset})..."
                : "Pushing local SQLite data to remote PostgreSQL (batched, resumable)...");
            Console.WriteLine();

            if (session.CurrentStage > PushStage.MigrateSchema)
                ctx.Maps = await DatabasePushIdMaps.RebuildAsync(localDb, remoteDb);

            var stages = GetStagePipeline();
            foreach (var (stage, runner) in stages)
            {
                if (stage < session.CurrentStage)
                    continue;

                if (stage > session.CurrentStage)
                {
                    session.CurrentStage = stage;
                    session.StageOffset = 0;
                }

                if (stage == PushStage.MigrateSchema)
                {
                    await DatabasePushStageRunner.EnsureRemoteSchemaAsync(remoteDb, display, cancellationToken);
                    await AdvanceStageAsync(localDb, session, stage, 0, cancellationToken);
                    continue;
                }

                var count = await runner(ctx, cancellationToken);
                session.TotalRecordsPushed += count;
                await AdvanceStageAsync(localDb, session, stage, count, cancellationToken);
            }

            session.Status = PushSessionStatus.Completed;
            session.CurrentStage = PushStage.Done;
            session.StageOffset = 0;
            session.CompletedAt = DateTime.UtcNow;
            session.LastError = null;
            await DatabasePushSessionStore.SaveAsync(localDb, session, cancellationToken);

            Console.WriteLine();

            if (ctx.Errors.Count > 0)
            {
                display.PrintWarning(
                    $"Push completed with {ctx.Errors.Count} warnings. {session.TotalRecordsPushed} records pushed.");
                Logger.Warning("Push completed with {ErrorCount} warnings: {@Errors}", ctx.Errors.Count, ctx.Errors);
                return new ScrapeResult
                {
                    Success = true,
                    RecordsProcessed = (int)Math.Min(session.TotalRecordsPushed, int.MaxValue),
                    Message = $"Push completed with {ctx.Errors.Count} warnings. {session.TotalRecordsPushed} records pushed.",
                    Errors = ctx.Errors,
                };
            }

            return ScrapeResult.Succeeded(
                (int)Math.Min(session.TotalRecordsPushed, int.MaxValue),
                $"Successfully pushed {session.TotalRecordsPushed} records to PostgreSQL");
        }
        catch (Exception ex)
        {
            session.Status = PushSessionStatus.Failed;
            session.LastError = ex.Message;
            await DatabasePushSessionStore.SaveAsync(localDb, session, cancellationToken);
            Logger.Error(ex, "Push to PostgreSQL failed at stage {Stage} offset {Offset}",
                session.CurrentStage, session.StageOffset);
            return ScrapeResult.Failed($"Push failed at {session.CurrentStage}: {ex.Message}");
        }
    }

    public Task<DatabasePushSession?> GetSessionStatusAsync(
        AppDbContext localDb,
        CancellationToken cancellationToken = default)
        => DatabasePushSessionStore.GetLatestAsync(localDb, cancellationToken);

    private static async Task AdvanceStageAsync(
        AppDbContext localDb,
        DatabasePushSession session,
        PushStage completedStage,
        int stageRecords,
        CancellationToken cancellationToken)
    {
        session.CurrentStage = completedStage + 1;
        session.StageOffset = 0;
        session.UpdatedAt = DateTime.UtcNow;
        await DatabasePushSessionStore.SaveAsync(localDb, session, cancellationToken);
    }

    private static IReadOnlyList<(PushStage Stage, Func<PushExecutionContext, CancellationToken, Task<int>> Runner)> GetStagePipeline()
        => new (PushStage, Func<PushExecutionContext, CancellationToken, Task<int>>)[]
        {
            (PushStage.MigrateSchema, static (_, _) => Task.FromResult(0)),
            (PushStage.Teams, DatabasePushStageRunner.PushTeamsAsync),
            (PushStage.Franchises, DatabasePushStageRunner.PushFranchisesAsync),
            (PushStage.TeamSeasons, DatabasePushStageRunner.PushTeamSeasonsAsync),
            (PushStage.Players, DatabasePushStageRunner.PushPlayersAsync),
            (PushStage.Venues, DatabasePushStageRunner.PushVenuesAsync),
            (PushStage.Games, DatabasePushStageRunner.PushGamesAsync),
            (PushStage.PlayerGameStats, DatabasePushStageRunner.PushPlayerGameStatsAsync),
            (PushStage.TeamGameStats, DatabasePushStageRunner.PushTeamGameStatsAsync),
            (PushStage.Injuries, DatabasePushStageRunner.PushInjuriesAsync),
            (PushStage.ApiLinks, DatabasePushStageRunner.PushApiLinksAsync),
            (PushStage.GameDrives, DatabasePushStageRunner.PushGameDrivesAsync),
            (PushStage.ScoringPlays, DatabasePushStageRunner.PushScoringPlaysAsync),
            (PushStage.GameWeather, DatabasePushStageRunner.PushGameWeatherAsync),
            (PushStage.GameOfficials, DatabasePushStageRunner.PushGameOfficialsAsync),
            (PushStage.GameOdds, DatabasePushStageRunner.PushGameOddsAsync),
        };
}
