using System.Text.Json;
using WebScraper.Services.Scrapers.MySportsFeeds;

namespace WebScraper.Tests.Scrapers.MySportsFeeds;

public class MySportsFeedsPlayerServiceTests
{
    private static readonly string SamplePlayersJson = """
    {
        "players": [
            {
                "player": {
                    "id": 100,
                    "firstName": "Patrick",
                    "lastName": "Mahomes",
                    "position": "QB",
                    "jerseyNumber": 15,
                    "height": "6-3",
                    "weight": 230,
                    "college": "Texas Tech",
                    "currentTeam": {
                        "abbreviation": "KC"
                    }
                }
            },
            {
                "player": {
                    "id": 200,
                    "firstName": "Travis",
                    "lastName": "Kelce",
                    "position": "TE",
                    "jerseyNumber": 87,
                    "height": "6-5",
                    "weight": 250,
                    "college": "Cincinnati",
                    "currentTeam": {
                        "abbreviation": "KC"
                    }
                }
            }
        ]
    }
    """;

    [Fact]
    public void MySportsFeedsPlayersResponse_ShouldDeserializeCorrectly()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = JsonSerializer.Deserialize<MySportsFeedsPlayersResponse>(SamplePlayersJson, options);

        Assert.NotNull(response);
        Assert.Equal(2, response.Players.Count);
    }

    [Fact]
    public void MySportsFeedsPlayer_ShouldHaveFirstAndLastName()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = JsonSerializer.Deserialize<MySportsFeedsPlayersResponse>(SamplePlayersJson, options);

        var mahomes = response!.Players[0].Player;
        Assert.Equal("Patrick", mahomes.FirstName);
        Assert.Equal("Mahomes", mahomes.LastName);

        // Name concatenation is done by the service (MapToPlayer), verify raw fields
        var fullName = $"{mahomes.FirstName} {mahomes.LastName}".Trim();
        Assert.Equal("Patrick Mahomes", fullName);
    }

    [Fact]
    public void MySportsFeedsPlayer_ShouldDeserializeAllFields()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = JsonSerializer.Deserialize<MySportsFeedsPlayersResponse>(SamplePlayersJson, options);

        var mahomes = response!.Players[0].Player;
        Assert.Equal("QB", mahomes.Position);
        Assert.Equal(15, mahomes.JerseyNumber);
        Assert.Equal("6-3", mahomes.Height);
        Assert.Equal(230, mahomes.Weight);
        Assert.Equal("Texas Tech", mahomes.College);
    }

    [Fact]
    public void MySportsFeedsPlayer_CurrentTeam_ShouldDeserialize()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = JsonSerializer.Deserialize<MySportsFeedsPlayersResponse>(SamplePlayersJson, options);

        var mahomes = response!.Players[0].Player;
        Assert.NotNull(mahomes.CurrentTeam);
        Assert.Equal("KC", mahomes.CurrentTeam.Abbreviation);
    }

    [Fact]
    public void MySportsFeedsPlayer_NullableFields_ShouldHandleNull()
    {
        var json = """
        {
            "players": [
                {
                    "player": {
                        "id": 999,
                        "firstName": "Test",
                        "lastName": "Player",
                        "position": null,
                        "jerseyNumber": null,
                        "height": null,
                        "weight": null,
                        "college": null,
                        "currentTeam": null
                    }
                }
            ]
        }
        """;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = JsonSerializer.Deserialize<MySportsFeedsPlayersResponse>(json, options);

        var player = response!.Players[0].Player;
        Assert.Null(player.Position);
        Assert.Null(player.JerseyNumber);
        Assert.Null(player.Height);
        Assert.Null(player.Weight);
        Assert.Null(player.College);
        Assert.Null(player.CurrentTeam);
    }

    [Fact]
    public void MySportsFeedsPlayer_EmptyNames_ShouldProduceEmptyFullName()
    {
        var json = """
        {
            "players": [
                {
                    "player": {
                        "id": 999,
                        "firstName": "",
                        "lastName": ""
                    }
                }
            ]
        }
        """;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = JsonSerializer.Deserialize<MySportsFeedsPlayersResponse>(json, options);

        var player = response!.Players[0].Player;
        var fullName = $"{player.FirstName} {player.LastName}".Trim();
        Assert.Equal("", fullName);
    }

    // --- GameLogs (Stats) deserialization ---

    private static readonly string SampleGameLogsJson = """
    {
        "gamelogs": [
            {
                "player": {
                    "id": 100,
                    "firstName": "Patrick",
                    "lastName": "Mahomes"
                },
                "game": {
                    "id": 1001,
                    "week": 1,
                    "homeTeam": { "abbreviation": "KC" },
                    "awayTeam": { "abbreviation": "BUF" }
                },
                "stats": {
                    "passing": {
                        "passCompletions": 22,
                        "passAttempts": 31,
                        "passYards": 292,
                        "passTD": 3,
                        "passInt": 1
                    },
                    "rushing": {
                        "rushAttempts": 4,
                        "rushYards": 18,
                        "rushTD": 0
                    },
                    "receiving": null
                }
            }
        ]
    }
    """;

    [Fact]
    public void MySportsFeedsGameLogsResponse_ShouldDeserializeCorrectly()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = JsonSerializer.Deserialize<MySportsFeedsGameLogsResponse>(SampleGameLogsJson, options);

        Assert.NotNull(response);
        Assert.Single(response.Gamelogs);
    }

    [Fact]
    public void MySportsFeedsGameLog_ShouldDeserializePassingStats()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = JsonSerializer.Deserialize<MySportsFeedsGameLogsResponse>(SampleGameLogsJson, options);

        var passing = response!.Gamelogs[0].Stats.Passing;
        Assert.NotNull(passing);
        Assert.Equal(22, passing.PassCompletions);
        Assert.Equal(31, passing.PassAttempts);
        Assert.Equal(292, passing.PassYards);
        Assert.Equal(3, passing.PassTouchdowns);
        Assert.Equal(1, passing.Interceptions);
    }

    [Fact]
    public void MySportsFeedsGameLog_ShouldDeserializeRushingStats()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = JsonSerializer.Deserialize<MySportsFeedsGameLogsResponse>(SampleGameLogsJson, options);

        var rushing = response!.Gamelogs[0].Stats.Rushing;
        Assert.NotNull(rushing);
        Assert.Equal(4, rushing.RushAttempts);
        Assert.Equal(18, rushing.RushYards);
        Assert.Equal(0, rushing.RushTouchdowns);
    }

    [Fact]
    public void MySportsFeedsGameLog_NullReceiving_ShouldBeNull()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var response = JsonSerializer.Deserialize<MySportsFeedsGameLogsResponse>(SampleGameLogsJson, options);

        Assert.Null(response!.Gamelogs[0].Stats.Receiving);
    }
}
