using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Tests.Helpers;

namespace WebScraper.Tests.Repositories;

public class PlayerRepositoryTests : IDisposable
{
    private readonly Data.AppDbContext _context;
    private readonly PlayerRepository _playerRepo;
    private readonly TeamRepository _teamRepo;

    public PlayerRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _playerRepo = new PlayerRepository(_context);
        _teamRepo = new TeamRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    private async Task<Team> SeedTeamAsync()
    {
        return await _teamRepo.AddAsync(new Team
        {
            Name = "Kansas City Chiefs", Abbreviation = "KC",
            City = "Kansas City", Conference = "AFC", Division = "West"
        });
    }

    [Fact]
    public async Task AddAsync_ShouldInsertPlayer()
    {
        var team = await SeedTeamAsync();
        var player = new Player { Name = "Patrick Mahomes", TeamId = team.Id, Position = "QB" };

        var result = await _playerRepo.AddAsync(player);

        Assert.True(result.Id > 0);
        Assert.Equal("Patrick Mahomes", result.Name);
    }

    [Fact]
    public async Task GetByTeamAsync_ShouldReturnPlayersForTeam()
    {
        var team = await SeedTeamAsync();
        await _playerRepo.AddAsync(new Player { Name = "Player 1", TeamId = team.Id, Position = "QB" });
        await _playerRepo.AddAsync(new Player { Name = "Player 2", TeamId = team.Id, Position = "RB" });

        var result = (await _playerRepo.GetByTeamAsync(team.Id)).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByNameAsync_ShouldFindPlayer()
    {
        var team = await SeedTeamAsync();
        await _playerRepo.AddAsync(new Player { Name = "Travis Kelce", TeamId = team.Id, Position = "TE" });

        var result = await _playerRepo.GetByNameAsync("Travis Kelce");

        Assert.NotNull(result);
        Assert.Equal("TE", result.Position);
    }

    [Fact]
    public async Task UpsertAsync_ShouldInsert_WhenNew()
    {
        var team = await SeedTeamAsync();
        var player = new Player { Name = "New Player", TeamId = team.Id, Position = "WR", JerseyNumber = 11 };

        await _playerRepo.UpsertAsync(player);

        var result = await _playerRepo.GetByNameAsync("New Player");
        Assert.NotNull(result);
        Assert.Equal(11, result.JerseyNumber);
    }

    [Fact]
    public async Task UpsertAsync_ShouldUpdate_WhenExisting()
    {
        var team = await SeedTeamAsync();
        await _playerRepo.AddAsync(new Player { Name = "Update Me", TeamId = team.Id, Position = "QB", JerseyNumber = 1 });

        var updated = new Player { Name = "Update Me", TeamId = team.Id, Position = "WR", JerseyNumber = 99, Weight = 210 };
        await _playerRepo.UpsertAsync(updated);

        var result = await _playerRepo.GetByNameAsync("Update Me");
        Assert.NotNull(result);
        Assert.Equal("WR", result.Position);
        Assert.Equal(99, result.JerseyNumber);
        Assert.Equal(210, result.Weight);
    }

    [Fact]
    public async Task Player_CanHaveNullTeamId()
    {
        var player = new Player { Name = "Free Agent", TeamId = null, Position = "CB" };

        var result = await _playerRepo.AddAsync(player);

        Assert.True(result.Id > 0);
        Assert.Null(result.TeamId);
    }
}
