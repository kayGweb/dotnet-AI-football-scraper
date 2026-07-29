using WebScraper.Data;
using WebScraper.Models;
using WebScraper.Services.Push;
using WebScraper.Tests.Helpers;

namespace WebScraper.Tests.Services;

public class DatabasePushSessionStoreTests
{
    [Fact]
    public async Task PrepareAsync_CreatesNewSession_WhenNoneExists()
    {
        await using var db = TestDbContextFactory.Create();
        var session = await DatabasePushSessionStore.PrepareAsync(db, PushOptions.Default);

        Assert.Equal(PushSessionStatus.InProgress, session.Status);
        Assert.Equal(PushStage.MigrateSchema, session.CurrentStage);
        Assert.Equal(0, session.StageOffset);
    }

    [Fact]
    public async Task PrepareAsync_Resumes_WhenSessionFailed()
    {
        await using var db = TestDbContextFactory.Create();
        db.DatabasePushSessions.Add(new DatabasePushSession
        {
            Status = PushSessionStatus.Failed,
            CurrentStage = PushStage.Games,
            StageOffset = 250,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow.AddHours(-1),
        });
        await db.SaveChangesAsync();

        var session = await DatabasePushSessionStore.PrepareAsync(db, new PushOptions { Resume = true });

        Assert.Equal(PushSessionStatus.InProgress, session.Status);
        Assert.Equal(PushStage.Games, session.CurrentStage);
        Assert.Equal(250, session.StageOffset);
    }

    [Fact]
    public async Task PrepareAsync_Throws_WhenInProgressWithoutResume()
    {
        await using var db = TestDbContextFactory.Create();
        db.DatabasePushSessions.Add(new DatabasePushSession
        {
            Status = PushSessionStatus.InProgress,
            CurrentStage = PushStage.Players,
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DatabasePushSessionStore.PrepareAsync(db, PushOptions.Default));
    }

    [Fact]
    public async Task PrepareAsync_Reset_ClearsExistingSession()
    {
        await using var db = TestDbContextFactory.Create();
        db.DatabasePushSessions.Add(new DatabasePushSession
        {
            Status = PushSessionStatus.Failed,
            CurrentStage = PushStage.Games,
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var session = await DatabasePushSessionStore.PrepareAsync(db, new PushOptions { Reset = true });

        Assert.Equal(PushStage.MigrateSchema, session.CurrentStage);
        Assert.Single(db.DatabasePushSessions);
    }
}
