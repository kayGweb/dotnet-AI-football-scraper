using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Tests.Helpers;

namespace WebScraper.Tests.Repositories;

public class GameRepositoryTests : IDisposable
{
    private readonly Data.AppDbContext _context;
    private readonly GameRepository _gameRepo;

    public GameRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _gameRepo = new GameRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task AddAsync_ShouldInsertGame()
    {
        var (home, away, _, _) = await RepositoryTestHelpers.SeedTeamSeasonsAsync(_context);
        var game = new Game
        {
            Season = 2025, Week = 1, GameDate = new DateTime(2025, 9, 7),
            HomeTeamSeasonId = home.Id, AwayTeamSeasonId = away.Id,
            HomeScore = 27, AwayScore = 24
        };

        var result = await _gameRepo.AddAsync(game);

        Assert.True(result.Id > 0);
        Assert.Equal(27, result.HomeScore);
    }

    [Fact]
    public async Task GetBySeasonAsync_ShouldReturnGamesForSeason()
    {
        var (home, away, _, _) = await RepositoryTestHelpers.SeedTeamSeasonsAsync(_context);
        await _gameRepo.AddAsync(new Game { Season = 2025, Week = 1, GameDate = DateTime.Now, HomeTeamSeasonId = home.Id, AwayTeamSeasonId = away.Id });
        await _gameRepo.AddAsync(new Game { Season = 2025, Week = 2, GameDate = DateTime.Now, HomeTeamSeasonId = away.Id, AwayTeamSeasonId = home.Id });
        await _gameRepo.AddAsync(new Game { Season = 2024, Week = 1, GameDate = DateTime.Now, HomeTeamSeasonId = home.Id, AwayTeamSeasonId = away.Id });

        var result = (await _gameRepo.GetBySeasonAsync(2025)).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, g => Assert.Equal(2025, g.Season));
    }

    [Fact]
    public async Task GetByWeekAsync_ShouldFilterBySeasonAndWeek()
    {
        var (home, away, _, _) = await RepositoryTestHelpers.SeedTeamSeasonsAsync(_context);
        await _gameRepo.AddAsync(new Game { Season = 2025, Week = 1, GameDate = DateTime.Now, HomeTeamSeasonId = home.Id, AwayTeamSeasonId = away.Id });
        await _gameRepo.AddAsync(new Game { Season = 2025, Week = 2, GameDate = DateTime.Now, HomeTeamSeasonId = away.Id, AwayTeamSeasonId = home.Id });

        var result = (await _gameRepo.GetByWeekAsync(2025, 1)).ToList();

        Assert.Single(result);
        Assert.Equal(1, result[0].Week);
    }

    [Fact]
    public async Task UpsertAsync_ShouldInsert_WhenNew()
    {
        var (home, away, _, _) = await RepositoryTestHelpers.SeedTeamSeasonsAsync(_context);
        var game = new Game
        {
            Season = 2025, Week = 5, GameDate = new DateTime(2025, 10, 12),
            HomeTeamSeasonId = home.Id, AwayTeamSeasonId = away.Id, HomeScore = 31, AwayScore = 17
        };

        await _gameRepo.UpsertAsync(game);

        var result = (await _gameRepo.GetByWeekAsync(2025, 5)).ToList();
        Assert.Single(result);
        Assert.Equal(31, result[0].HomeScore);
    }

    [Fact]
    public async Task UpsertAsync_ShouldUpdate_WhenExisting()
    {
        var (home, away, _, _) = await RepositoryTestHelpers.SeedTeamSeasonsAsync(_context);
        await _gameRepo.AddAsync(new Game
        {
            Season = 2025, Week = 3, GameDate = new DateTime(2025, 9, 21),
            HomeTeamSeasonId = home.Id, AwayTeamSeasonId = away.Id, HomeScore = null, AwayScore = null
        });

        var updated = new Game
        {
            Season = 2025, Week = 3, GameDate = new DateTime(2025, 9, 21),
            HomeTeamSeasonId = home.Id, AwayTeamSeasonId = away.Id, HomeScore = 24, AwayScore = 20
        };
        await _gameRepo.UpsertAsync(updated);

        var result = (await _gameRepo.GetByWeekAsync(2025, 3)).ToList();
        Assert.Single(result);
        Assert.Equal(24, result[0].HomeScore);
        Assert.Equal(20, result[0].AwayScore);
    }
}
