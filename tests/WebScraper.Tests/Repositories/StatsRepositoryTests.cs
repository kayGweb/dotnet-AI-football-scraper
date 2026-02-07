using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Tests.Helpers;

namespace WebScraper.Tests.Repositories;

public class StatsRepositoryTests : IDisposable
{
    private readonly Data.AppDbContext _context;
    private readonly StatsRepository _statsRepo;
    private readonly TeamRepository _teamRepo;
    private readonly PlayerRepository _playerRepo;
    private readonly GameRepository _gameRepo;

    public StatsRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _statsRepo = new StatsRepository(_context);
        _teamRepo = new TeamRepository(_context);
        _playerRepo = new PlayerRepository(_context);
        _gameRepo = new GameRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    private async Task<(Player player, Game game)> SeedDataAsync()
    {
        var homeTeam = await _teamRepo.AddAsync(new Team
        {
            Name = "Kansas City Chiefs", Abbreviation = "KC",
            City = "Kansas City", Conference = "AFC", Division = "West"
        });
        var awayTeam = await _teamRepo.AddAsync(new Team
        {
            Name = "Buffalo Bills", Abbreviation = "BUF",
            City = "Buffalo", Conference = "AFC", Division = "East"
        });
        var player = await _playerRepo.AddAsync(new Player
        {
            Name = "Patrick Mahomes", TeamId = homeTeam.Id, Position = "QB"
        });
        var game = await _gameRepo.AddAsync(new Game
        {
            Season = 2025, Week = 1, GameDate = new DateTime(2025, 9, 7),
            HomeTeamId = homeTeam.Id, AwayTeamId = awayTeam.Id,
            HomeScore = 27, AwayScore = 24
        });
        return (player, game);
    }

    [Fact]
    public async Task UpsertAsync_ShouldInsert_WhenNew()
    {
        var (player, game) = await SeedDataAsync();
        var stats = new PlayerGameStats
        {
            PlayerId = player.Id, GameId = game.Id,
            PassAttempts = 35, PassCompletions = 25, PassYards = 312,
            PassTouchdowns = 3, Interceptions = 1
        };

        await _statsRepo.UpsertAsync(stats);

        var result = (await _statsRepo.GetGameStatsAsync(game.Id)).ToList();
        Assert.Single(result);
        Assert.Equal(312, result[0].PassYards);
    }

    [Fact]
    public async Task UpsertAsync_ShouldUpdate_WhenExisting()
    {
        var (player, game) = await SeedDataAsync();
        await _statsRepo.AddAsync(new PlayerGameStats
        {
            PlayerId = player.Id, GameId = game.Id,
            PassAttempts = 30, PassCompletions = 20, PassYards = 250,
            PassTouchdowns = 2, Interceptions = 0
        });

        // Upsert with corrected stats
        var updated = new PlayerGameStats
        {
            PlayerId = player.Id, GameId = game.Id,
            PassAttempts = 35, PassCompletions = 25, PassYards = 312,
            PassTouchdowns = 3, Interceptions = 1
        };
        await _statsRepo.UpsertAsync(updated);

        var result = (await _statsRepo.GetGameStatsAsync(game.Id)).ToList();
        Assert.Single(result);
        Assert.Equal(312, result[0].PassYards);
        Assert.Equal(3, result[0].PassTouchdowns);
    }

    [Fact]
    public async Task GetPlayerStatsAsync_ShouldFilterByPlayerAndSeason()
    {
        var (player, game) = await SeedDataAsync();
        await _statsRepo.AddAsync(new PlayerGameStats
        {
            PlayerId = player.Id, GameId = game.Id,
            PassAttempts = 30, PassCompletions = 22, PassYards = 280,
            PassTouchdowns = 2, Interceptions = 0
        });

        var result = (await _statsRepo.GetPlayerStatsAsync("Patrick Mahomes", 2025)).ToList();

        Assert.Single(result);
        Assert.Equal(280, result[0].PassYards);
    }

    [Fact]
    public async Task GetPlayerStatsAsync_ShouldReturnEmpty_WhenNoMatch()
    {
        await SeedDataAsync();

        var result = (await _statsRepo.GetPlayerStatsAsync("Nobody", 2025)).ToList();

        Assert.Empty(result);
    }
}
