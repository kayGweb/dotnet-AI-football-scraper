using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Tests.Helpers;

namespace WebScraper.Tests.Repositories;

public class TeamRepositoryTests : IDisposable
{
    private readonly Data.AppDbContext _context;
    private readonly TeamRepository _repository;

    public TeamRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _repository = new TeamRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task AddAsync_ShouldInsertTeam()
    {
        var team = new Team
        {
            Name = "Kansas City Chiefs",
            Abbreviation = "KC",
            City = "Kansas City",
            Conference = "AFC",
            Division = "West"
        };

        var result = await _repository.AddAsync(team);

        Assert.True(result.Id > 0);
        Assert.Equal("KC", result.Abbreviation);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnTeam()
    {
        var team = new Team
        {
            Name = "San Francisco 49ers",
            Abbreviation = "SF",
            City = "San Francisco",
            Conference = "NFC",
            Division = "West"
        };
        await _repository.AddAsync(team);

        var result = await _repository.GetByIdAsync(team.Id);

        Assert.NotNull(result);
        Assert.Equal("SF", result.Abbreviation);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotFound()
    {
        var result = await _repository.GetByIdAsync(999);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllTeams()
    {
        await _repository.AddAsync(new Team { Name = "Team A", Abbreviation = "A", City = "City A", Conference = "AFC", Division = "East" });
        await _repository.AddAsync(new Team { Name = "Team B", Abbreviation = "B", City = "City B", Conference = "NFC", Division = "West" });

        var result = (await _repository.GetAllAsync()).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByAbbreviationAsync_ShouldFindTeam()
    {
        await _repository.AddAsync(new Team { Name = "Buffalo Bills", Abbreviation = "BUF", City = "Buffalo", Conference = "AFC", Division = "East" });

        var result = await _repository.GetByAbbreviationAsync("BUF");

        Assert.NotNull(result);
        Assert.Equal("Buffalo Bills", result.Name);
    }

    [Fact]
    public async Task GetByAbbreviationAsync_ShouldReturnNull_WhenNotFound()
    {
        var result = await _repository.GetByAbbreviationAsync("XYZ");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByConferenceAsync_ShouldFilterByConference()
    {
        await _repository.AddAsync(new Team { Name = "Team AFC", Abbreviation = "A1", City = "City", Conference = "AFC", Division = "East" });
        await _repository.AddAsync(new Team { Name = "Team NFC", Abbreviation = "N1", City = "City", Conference = "NFC", Division = "East" });
        await _repository.AddAsync(new Team { Name = "Team AFC2", Abbreviation = "A2", City = "City", Conference = "AFC", Division = "West" });

        var result = (await _repository.GetByConferenceAsync("AFC")).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.Equal("AFC", t.Conference));
    }

    [Fact]
    public async Task UpsertAsync_ShouldInsert_WhenNew()
    {
        var team = new Team { Name = "New Team", Abbreviation = "NEW", City = "New City", Conference = "AFC", Division = "North" };

        await _repository.UpsertAsync(team);

        var result = await _repository.GetByAbbreviationAsync("NEW");
        Assert.NotNull(result);
        Assert.Equal("New Team", result.Name);
    }

    [Fact]
    public async Task UpsertAsync_ShouldUpdate_WhenExisting()
    {
        await _repository.AddAsync(new Team { Name = "Old Name", Abbreviation = "TST", City = "Old City", Conference = "AFC", Division = "East" });

        var updated = new Team { Name = "New Name", Abbreviation = "TST", City = "New City", Conference = "NFC", Division = "West" };
        await _repository.UpsertAsync(updated);

        var result = await _repository.GetByAbbreviationAsync("TST");
        Assert.NotNull(result);
        Assert.Equal("New Name", result.Name);
        Assert.Equal("New City", result.City);
        Assert.Equal("NFC", result.Conference);
        Assert.Equal("West", result.Division);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveTeam()
    {
        var team = await _repository.AddAsync(new Team { Name = "Delete Me", Abbreviation = "DEL", City = "Gone", Conference = "AFC", Division = "East" });

        await _repository.DeleteAsync(team.Id);

        var result = await _repository.GetByIdAsync(team.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrue_WhenExists()
    {
        var team = await _repository.AddAsync(new Team { Name = "Exists", Abbreviation = "EX", City = "Here", Conference = "AFC", Division = "East" });

        Assert.True(await _repository.ExistsAsync(team.Id));
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalse_WhenNotExists()
    {
        Assert.False(await _repository.ExistsAsync(999));
    }
}
