using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WebScraper.Models;
using WebScraper.Services.Coverage;
using WebScraper.Tests.Helpers;

namespace WebScraper.Tests.Services;

public class BackfillOrchestratorTests
{
    [Fact]
    public async Task FanOutAsync_CreatesChildJobs_ForSingleSeason()
    {
        await using var db = TestDbContextFactory.Create();
        var parent = new ScrapeJob
        {
            Type = ScrapeJobType.Backfill,
            Source = "Espn",
            Season = 2024,
            Week = 2024,
            Status = ScrapeJobStatus.Running,
            CreatedAt = DateTime.UtcNow,
        };
        db.ScrapeJobs.Add(parent);
        await db.SaveChangesAsync();

        var orchestrator = new BackfillOrchestrator(db, NullLogger<BackfillOrchestrator>.Instance);
        var result = await orchestrator.FanOutAsync(parent, 2024, 2024);

        Assert.True(result.AllChildIds.Count > 0);
        Assert.NotEmpty(result.InitialEnqueueIds);

        var children = db.ScrapeJobs.Where(j => j.ParentJobId == parent.Id).ToList();
        Assert.Equal(result.AllChildIds.Count, children.Count);
        Assert.All(children, c => Assert.Equal(parent.Id, c.ParentJobId));
    }

    [Fact]
    public async Task FanOutAsync_IsIdempotent_WhenChildrenAlreadyExist()
    {
        await using var db = TestDbContextFactory.Create();
        var parent = new ScrapeJob
        {
            Type = ScrapeJobType.Backfill,
            Source = "Espn",
            Season = 2024,
            Week = 2024,
            Status = ScrapeJobStatus.Running,
            CreatedAt = DateTime.UtcNow,
        };
        db.ScrapeJobs.Add(parent);
        await db.SaveChangesAsync();

        var orchestrator = new BackfillOrchestrator(db, NullLogger<BackfillOrchestrator>.Instance);
        var first = await orchestrator.FanOutAsync(parent, 2024, 2024);
        var second = await orchestrator.FanOutAsync(parent, 2024, 2024);

        Assert.Equal(first.AllChildIds.Count, second.AllChildIds.Count);
        Assert.Equal(first.AllChildIds.OrderBy(x => x), second.AllChildIds.OrderBy(x => x));

        var totalChildren = db.ScrapeJobs.Count(j => j.ParentJobId == parent.Id);
        Assert.Equal(first.AllChildIds.Count, totalChildren);
    }

    [Fact]
    public async Task GetProgressAsync_ReturnsAggregateCounts()
    {
        await using var db = TestDbContextFactory.Create();
        var parent = new ScrapeJob
        {
            Type = ScrapeJobType.Backfill,
            Source = "Espn",
            Season = 2024,
            Week = 2024,
            Status = ScrapeJobStatus.Running,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            CreatedAt = DateTime.UtcNow,
        };
        db.ScrapeJobs.Add(parent);
        await db.SaveChangesAsync();

        db.ScrapeJobs.AddRange(
            new ScrapeJob { Type = ScrapeJobType.Games, ParentJobId = parent.Id, Status = ScrapeJobStatus.Succeeded, CreatedAt = DateTime.UtcNow },
            new ScrapeJob { Type = ScrapeJobType.Stats, ParentJobId = parent.Id, Status = ScrapeJobStatus.Queued, CreatedAt = DateTime.UtcNow },
            new ScrapeJob { Type = ScrapeJobType.Games, ParentJobId = parent.Id, Status = ScrapeJobStatus.Failed, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var orchestrator = new BackfillOrchestrator(db, NullLogger<BackfillOrchestrator>.Instance);
        var progress = await orchestrator.GetProgressAsync(parent.Id);

        Assert.Equal(3, progress.TotalChildJobs);
        Assert.Equal(1, progress.Succeeded);
        Assert.Equal(1, progress.Failed);
        Assert.Equal(1, progress.Queued);
    }
}

public class BackfillProgressCalculatorTests
{
    [Fact]
    public void GetEnqueueableChildIds_RespectsDependencyOrder()
    {
        var children = new List<ScrapeJob>
        {
            new() { Id = 1, Type = ScrapeJobType.Games, Status = ScrapeJobStatus.Succeeded },
            new() { Id = 2, Type = ScrapeJobType.Stats, Status = ScrapeJobStatus.Queued, DependsOnJobId = 1 },
            new() { Id = 3, Type = ScrapeJobType.Stats, Status = ScrapeJobStatus.Queued, DependsOnJobId = 99 },
        };

        var enqueueable = BackfillProgressCalculator.GetEnqueueableChildIds(children);

        Assert.Single(enqueueable);
        Assert.Equal(2, enqueueable[0]);
    }
}
