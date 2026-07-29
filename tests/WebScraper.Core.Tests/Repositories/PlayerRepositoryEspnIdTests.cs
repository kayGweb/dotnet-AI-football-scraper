using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Tests.Helpers;

namespace WebScraper.Tests.Repositories;

public class PlayerRepositoryEspnIdTests : IDisposable
{
    private readonly Data.AppDbContext _context;
    private readonly PlayerRepository _playerRepo;
    private readonly TeamRepository _teamRepo;

    public PlayerRepositoryEspnIdTests()
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

    [Fact]
    public async Task UpsertByEspnIdAsync_MergesSamePlayerAcrossTeams()
    {
        var chiefs = await _teamRepo.AddAsync(new Team { Name = "Chiefs", Abbreviation = "KC", City = "KC", Conference = "AFC", Division = "West" });
        var jets = await _teamRepo.AddAsync(new Team { Name = "Jets", Abbreviation = "NYJ", City = "NY", Conference = "AFC", Division = "East" });

        await _playerRepo.UpsertByEspnIdAsync(new Player
        {
            EspnId = "12345",
            Name = "Player A",
            TeamId = chiefs.Id,
            Position = "QB",
        });

        var updated = await _playerRepo.UpsertByEspnIdAsync(new Player
        {
            EspnId = "12345",
            Name = "Player A",
            TeamId = jets.Id,
            Position = "QB",
        });

        var all = (await _playerRepo.GetAllAsync()).ToList();
        Assert.Single(all);
        Assert.Equal(jets.Id, updated.TeamId);
    }

    [Fact]
    public async Task UpsertAsync_UsesEspnId_WhenPresent()
    {
        var team = await _teamRepo.AddAsync(new Team { Name = "Chiefs", Abbreviation = "KC", City = "KC", Conference = "AFC", Division = "West" });

        await _playerRepo.UpsertAsync(new Player { EspnId = "99", Name = "Same Name", TeamId = team.Id, Position = "WR" });
        await _playerRepo.UpsertAsync(new Player { EspnId = "99", Name = "Same Name", TeamId = team.Id, Position = "WR", JerseyNumber = 11 });

        var player = await _playerRepo.GetByEspnIdAsync("99");
        Assert.NotNull(player);
        Assert.Equal(11, player.JerseyNumber);
    }
}
