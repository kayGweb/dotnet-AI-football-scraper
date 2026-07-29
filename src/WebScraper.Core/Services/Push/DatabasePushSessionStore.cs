using Microsoft.EntityFrameworkCore;
using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Services.Push;

public static class DatabasePushSessionStore
{
    public static async Task<DatabasePushSession> PrepareAsync(
        AppDbContext db,
        PushOptions options,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.DatabasePushSessions
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (options.Reset && existing != null)
        {
            db.DatabasePushSessions.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
            existing = null;
        }

        if (options.Resume && existing is { Status: PushSessionStatus.InProgress or PushSessionStatus.Failed })
        {
            existing.Status = PushSessionStatus.InProgress;
            existing.LastError = null;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        if (existing is { Status: PushSessionStatus.InProgress or PushSessionStatus.Failed })
        {
            throw new InvalidOperationException(
                "A push session is already in progress. Pass resume=true to continue or reset=true to start over.");
        }

        var session = new DatabasePushSession
        {
            Status = PushSessionStatus.InProgress,
            CurrentStage = PushStage.MigrateSchema,
            StageOffset = 0,
            TotalRecordsPushed = 0,
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.DatabasePushSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public static async Task SaveAsync(
        AppDbContext db,
        DatabasePushSession session,
        CancellationToken cancellationToken = default)
    {
        session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task<DatabasePushSession?> GetLatestAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
        => await db.DatabasePushSessions
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
