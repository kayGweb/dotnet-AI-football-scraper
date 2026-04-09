using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Tests.Helpers;

namespace WebScraper.Tests.Repositories;

public class GameRepositoryTests : IDisposable
{
    private readonly Data.AppDbContext _context;
    private readonly GameRepository _gameRepo;
    private readonly TeamRepository _teamRepo;

    public GameRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _gameRepo = new GameRepository(_context);
        _teamRepo = new TeamRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    private async Task<(Team home, Team away)> SeedTeamsAsync()
    {
        var home = await _teamRepo.AddAsync(new Team
        {
            Name = "Kansas City Chiefs", Abbreviation = "KC",
            City = "Kansas City", Conference = "AFC", Division = "West"
        });
        var away = await _teamRepo.AddAsync(new Team
        {
            Name = "Buffalo Bills", Abbreviation = "BUF",
            City = "Buffalo", Conference = "AFC", Division = "East"
        });
        return (home, away);
    }

    [Fact]
    public async Task AddAsync_ShouldInsertGame()
    {
        var (home, away) = await SeedTeamsAsync();
        var game = new Game
        {
            Season = 2025, Week = 1, GameDate = new DateTime(2025, 9, 7),
            HomeTeamId = home.Id, AwayTeamId = away.Id,
            HomeScore = 27, AwayScore = 24
        };

        var result = await _gameRepo.AddAsync(game);

        Assert.True(result.Id > 0);
        Assert.Equal(27, result.HomeScore);
    }

    [Fact]
    public async Task GetBySeasonAsync_ShouldReturnGamesForSeason()
    {
        var (home, away) = await SeedTeamsAsync();
        await _gameRepo.AddAsync(new Game { Season = 2025, Week = 1, GameDate = DateTime.Now, HomeTeamId = home.Id, AwayTeamId = away.Id });
        await _gameRepo.AddAsync(new Game { Season = 2025, Week = 2, GameDate = DateTime.Now, HomeTeamId = away.Id, AwayTeamId = home.Id });
        await _gameRepo.AddAsync(new Game { Season = 2024, Week = 1, GameDate = DateTime.Now, HomeTeamId = home.Id, AwayTeamId = away.Id });

        var result = (await _gameRepo.GetBySeasonAsync(2025)).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, g => Assert.Equal(2025, g.Season));
    }

    [Fact]
    public async Task GetByWeekAsync_ShouldFilterBySeasonAndWeek()
    {
        var (home, away) = await SeedTeamsAsync();
        await _gameRepo.AddAsync(new Game { Season = 2025, Week = 1, GameDate = DateTime.Now, HomeTeamId = home.Id, AwayTeamId = away.Id });
        await _gameRepo.AddAsync(new Game { Season = 2025, Week = 2, GameDate = DateTime.Now, HomeTeamId = away.Id, AwayTeamId = home.Id });

        var result = (await _gameRepo.GetByWeekAsync(2025, 1)).ToList();

        Assert.Single(result);
        Assert.Equal(1, result[0].Week);
    }

    [Fact]
    public async Task UpsertAsync_ShouldInsert_WhenNew()
    {
        var (home, away) = await SeedTeamsAsync();
        var game = new Game
        {
            Season = 2025, Week = 5, GameDate = new DateTime(2025, 10, 12),
            HomeTeamId = home.Id, AwayTeamId = away.Id, HomeScore = 31, AwayScore = 17
        };

        await _gameRepo.UpsertAsync(game);

        var result = (await _gameRepo.GetByWeekAsync(2025, 5)).ToList();
        Assert.Single(result);
        Assert.Equal(31, result[0].HomeScore);
    }

    [Fact]
    public async Task UpsertAsync_ShouldUpdate_WhenExisting()
    {
        var (home, away) = await SeedTeamsAsync();
        await _gameRepo.AddAsync(new Game
        {
            Season = 2025, Week = 3, GameDate = new DateTime(2025, 9, 21),
            HomeTeamId = home.Id, AwayTeamId = away.Id, HomeScore = null, AwayScore = null
        });

        // Update with final scores
        var updated = new Game
        {
            Season = 2025, Week = 3, GameDate = new DateTime(2025, 9, 21),
            HomeTeamId = home.Id, AwayTeamId = away.Id, HomeScore = 24, AwayScore = 20
        };
        await _gameRepo.UpsertAsync(updated);

        var result = (await _gameRepo.GetByWeekAsync(2025, 3)).ToList();
        Assert.Single(result);
        Assert.Equal(24, result[0].HomeScore);
        Assert.Equal(20, result[0].AwayScore);
    }
}
