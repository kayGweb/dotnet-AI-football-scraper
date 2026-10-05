using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Tests.Helpers;

namespace WebScraper.Tests.Repositories;

public class InjuryReportRepositoryTests : IDisposable
{
    private readonly Data.AppDbContext _context;
    private readonly InjuryReportRepository _repo;

    public InjuryReportRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _repo = new InjuryReportRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsLatestSnapshotPerExternalPlayerId()
    {
        var (home, _, _, _) = await RepositoryTestHelpers.SeedTeamSeasonsAsync(_context);
        var older = new DateTime(2025, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2025, 10, 3, 12, 0, 0, DateTimeKind.Utc);

        await _repo.UpsertAsync(new InjuryReport
        {
            TeamSeasonId = home.Id,
            ExternalPlayerId = "apisports-99",
            PlayerName = "Test Player",
            Position = "QB",
            Status = "Out",
            SnapshotAt = older,
        });
        await _repo.UpsertAsync(new InjuryReport
        {
            TeamSeasonId = home.Id,
            ExternalPlayerId = "apisports-99",
            PlayerName = "Test Player",
            Position = "QB",
            Status = "Questionable",
            SnapshotAt = newer,
        });

        var current = await _repo.GetCurrentAsync();

        var report = Assert.Single(current);
        Assert.Equal("Questionable", report.Status);
        Assert.Equal(newer, report.SnapshotAt);
    }

    [Fact]
    public async Task GetCurrentAsync_FiltersByTeamAbbreviation()
    {
        var (home, away, _, _) = await RepositoryTestHelpers.SeedTeamSeasonsAsync(_context);
        var snapshot = new DateTime(2025, 10, 3, 12, 0, 0, DateTimeKind.Utc);

        await _repo.UpsertAsync(new InjuryReport
        {
            TeamSeasonId = home.Id,
            ExternalPlayerId = "apisports-kc",
            PlayerName = "KC Player",
            SnapshotAt = snapshot,
        });
        await _repo.UpsertAsync(new InjuryReport
        {
            TeamSeasonId = away.Id,
            ExternalPlayerId = "apisports-buf",
            PlayerName = "BUF Player",
            SnapshotAt = snapshot,
        });

        var kcOnly = await _repo.GetCurrentAsync("KC");

        Assert.Single(kcOnly);
        Assert.Equal("apisports-kc", kcOnly[0].ExternalPlayerId);
    }
}
