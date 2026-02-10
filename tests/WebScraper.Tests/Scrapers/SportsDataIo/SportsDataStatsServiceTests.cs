using System.Text.Json;
using WebScraper.Services.Scrapers.SportsDataIo;

namespace WebScraper.Tests.Scrapers.SportsDataIo;

public class SportsDataStatsServiceTests
{
    private static readonly string SampleStatsJson = """
    [
        {
            "PlayerID": 100,
            "Name": "Patrick Mahomes",
            "Team": "KC",
            "GameKey": "202501012",
            "PassingCompletions": 22,
            "PassingAttempts": 31,
            "PassingYards": 292,
            "PassingTouchdowns": 3,
            "PassingInterceptions": 1,
            "RushingAttempts": 4,
            "RushingYards": 18,
            "RushingTouchdowns": 0,
            "Receptions": 0,
            "ReceivingYards": 0,
            "ReceivingTouchdowns": 0
        },
        {
            "PlayerID": 200,
            "Name": "Travis Kelce",
            "Team": "KC",
            "GameKey": "202501012",
            "PassingCompletions": 0,
            "PassingAttempts": 0,
            "PassingYards": 0,
            "PassingTouchdowns": 0,
            "PassingInterceptions": 0,
            "RushingAttempts": 0,
            "RushingYards": 0,
            "RushingTouchdowns": 0,
            "Receptions": 7,
            "ReceivingYards": 89,
            "ReceivingTouchdowns": 1
        },
        {
            "PlayerID": 300,
            "Name": "No Stats Player",
            "Team": "KC",
            "GameKey": "202501012",
            "PassingCompletions": 0,
            "PassingAttempts": 0,
            "PassingYards": 0,
            "PassingTouchdowns": 0,
            "PassingInterceptions": 0,
            "RushingAttempts": 0,
            "RushingYards": 0,
            "RushingTouchdowns": 0,
            "Receptions": 0,
            "ReceivingYards": 0,
            "ReceivingTouchdowns": 0
        }
    ]
    """;

    [Fact]
    public void SportsDataPlayerStatsDto_ShouldDeserializeCorrectly()
    {
        var statsList = JsonSerializer.Deserialize<List<SportsDataPlayerStatsDto>>(SampleStatsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(statsList);
        Assert.Equal(3, statsList.Count);
    }

    [Fact]
    public void SportsDataPlayerStatsDto_ShouldMapPassingFieldsCorrectly()
    {
        var statsList = JsonSerializer.Deserialize<List<SportsDataPlayerStatsDto>>(SampleStatsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var mahomes = statsList!.First(s => s.Name == "Patrick Mahomes");
        Assert.Equal(22, mahomes.PassingCompletions);
        Assert.Equal(31, mahomes.PassingAttempts);
        Assert.Equal(292, mahomes.PassingYards);
        Assert.Equal(3, mahomes.PassingTouchdowns);
        Assert.Equal(1, mahomes.PassingInterceptions);
    }

    [Fact]
    public void SportsDataPlayerStatsDto_ShouldMapRushingFieldsCorrectly()
    {
        var statsList = JsonSerializer.Deserialize<List<SportsDataPlayerStatsDto>>(SampleStatsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var mahomes = statsList!.First(s => s.Name == "Patrick Mahomes");
        Assert.Equal(4, mahomes.RushingAttempts);
        Assert.Equal(18, mahomes.RushingYards);
        Assert.Equal(0, mahomes.RushingTouchdowns);
    }

    [Fact]
    public void SportsDataPlayerStatsDto_ShouldMapReceivingFieldsCorrectly()
    {
        var statsList = JsonSerializer.Deserialize<List<SportsDataPlayerStatsDto>>(SampleStatsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var kelce = statsList!.First(s => s.Name == "Travis Kelce");
        Assert.Equal(7, kelce.Receptions);
        Assert.Equal(89, kelce.ReceivingYards);
        Assert.Equal(1, kelce.ReceivingTouchdowns);
    }

    [Fact]
    public void SportsDataPlayerStatsDto_ZeroStats_ShouldDeserializeAsZero()
    {
        var statsList = JsonSerializer.Deserialize<List<SportsDataPlayerStatsDto>>(SampleStatsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var noStats = statsList!.First(s => s.Name == "No Stats Player");
        Assert.Equal(0, noStats.PassingAttempts);
        Assert.Equal(0, noStats.RushingAttempts);
        Assert.Equal(0, noStats.Receptions);
    }

    [Fact]
    public void SportsDataPlayerStatsDto_ShouldPreserveTeamAndGameKey()
    {
        var statsList = JsonSerializer.Deserialize<List<SportsDataPlayerStatsDto>>(SampleStatsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var mahomes = statsList!.First(s => s.Name == "Patrick Mahomes");
        Assert.Equal("KC", mahomes.Team);
        Assert.Equal("202501012", mahomes.GameKey);
    }
}
