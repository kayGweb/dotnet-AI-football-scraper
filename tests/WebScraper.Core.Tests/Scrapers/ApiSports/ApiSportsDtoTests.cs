using System.Text.Json;
using WebScraper.Services.Scrapers.ApiSports;

namespace WebScraper.Tests.Scrapers.ApiSports;

public class ApiSportsDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "ApiSports", fileName);

    [Fact]
    public void DeserializeGamesByDateFixture_MatchesCaptureShape()
    {
        var json = File.ReadAllText(FixturePath("games-by-date.json"));
        var envelope = JsonSerializer.Deserialize<ApiSportsEnvelope<ApiSportsGameItem>>(json, JsonOptions);

        Assert.NotNull(envelope);
        Assert.Equal("games", envelope.Get);
        Assert.Equal("2026-10-04", envelope.Parameters.GetProperty("date").GetString());
        Assert.False(envelope.HasErrors);
        Assert.Equal(16, envelope.Results);
        Assert.Equal(16, envelope.Response.Count);
    }

    [Fact]
    public void DeserializeGamesByDateFixture_NflAndNcaaCounts()
    {
        var json = File.ReadAllText(FixturePath("games-by-date.json"));
        var envelope = JsonSerializer.Deserialize<ApiSportsEnvelope<ApiSportsGameItem>>(json, JsonOptions)!;

        var nfl = envelope.Response.Where(ApiSportsMappings.IsNfl).ToList();
        Assert.Equal(13, nfl.Count);
    }

    [Fact]
    public void DeserializeGamesByDateFixture_CalPolyOvertimeScores()
    {
        var json = File.ReadAllText(FixturePath("games-by-date.json"));
        var envelope = JsonSerializer.Deserialize<ApiSportsEnvelope<ApiSportsGameItem>>(json, JsonOptions)!;

        var calPoly = envelope.Response.Single(g =>
            g.Teams.Home.Name == "Cal Poly" && g.Teams.Away.Name == "Weber State");

        Assert.Equal(7, calPoly.Scores.Home.Overtime);
        Assert.Equal(7, calPoly.Scores.Away.Overtime);
    }

    [Fact]
    public void DeserializeGamesByDateFixture_ScheduledGameHasNullScores()
    {
        var json = File.ReadAllText(FixturePath("games-by-date.json"));
        var envelope = JsonSerializer.Deserialize<ApiSportsEnvelope<ApiSportsGameItem>>(json, JsonOptions)!;

        var notStarted = envelope.Response.Single(g => g.Game.Status.Short == "NS");

        Assert.Null(notStarted.Scores.Home.Quarter1);
        Assert.Null(notStarted.Scores.Home.Quarter2);
        Assert.Null(notStarted.Scores.Home.Quarter3);
        Assert.Null(notStarted.Scores.Home.Quarter4);
        Assert.Null(notStarted.Scores.Home.Overtime);
        Assert.Null(notStarted.Scores.Home.Total);
        Assert.Null(notStarted.Scores.Away.Quarter1);
        Assert.Null(notStarted.Scores.Away.Total);
    }

    [Fact]
    public void DeserializePlayersByTeamFixture_MatchesCaptureShape()
    {
        var json = File.ReadAllText(FixturePath("players-by-team.json"));
        var envelope = JsonSerializer.Deserialize<ApiSportsEnvelope<ApiSportsPlayer>>(json, JsonOptions);

        Assert.NotNull(envelope);
        Assert.Equal("players", envelope.Get);
        Assert.Equal("2024", envelope.Parameters.GetProperty("season").GetString());
        Assert.Equal("1", envelope.Parameters.GetProperty("team").GetString());
        Assert.False(envelope.HasErrors);
        Assert.Equal(14, envelope.Results);
        Assert.Equal(14, envelope.Response.Count);
    }

    [Fact]
    public void DeserializeTeamsFixture_FlatTeamObjects()
    {
        var json = File.ReadAllText(FixturePath("teams.json"));
        var envelope = JsonSerializer.Deserialize<ApiSportsEnvelope<ApiSportsTeamProfile>>(json, JsonOptions);

        Assert.NotNull(envelope);
        Assert.Equal("teams", envelope.Get);
        Assert.False(envelope.HasErrors);
        Assert.Equal(32, envelope.Results);
        Assert.Equal(32, envelope.Response.Count);

        var raiders = envelope.Response.Single(t => t.Id == 1);
        Assert.Equal("Las Vegas Raiders", raiders.Name);
        Assert.Equal("LV", raiders.Code);
        Assert.Equal("Las Vegas", raiders.City);
    }

    [Fact]
    public void DeserializeGamePlayerStatsFixture_GroupsAndStringValues()
    {
        var json = File.ReadAllText(FixturePath("game-player-stats.json"));
        var envelope = JsonSerializer.Deserialize<ApiSportsEnvelope<ApiSportsPlayerStatsTeamBlock>>(json, JsonOptions);

        Assert.NotNull(envelope);
        Assert.Equal("games/statistics/players", envelope.Get);
        Assert.Equal("21572", envelope.Parameters.GetProperty("id").GetString());
        Assert.False(envelope.HasErrors);
        Assert.Equal(2, envelope.Response.Count);

        var raiders = envelope.Response.Single(b => b.Team.Id == 1);
        Assert.Equal(10, raiders.Groups.Count);

        var passing = raiders.Groups.Single(g => g.Name == "Passing");
        var qb = Assert.Single(passing.Players);
        Assert.Equal(1001, qb.Player.Id);
        Assert.Equal("Geno Smith", qb.Player.Name);
        Assert.Equal("24/36", qb.Statistics.Single(s => s.Name == "comp_att").Text);
        Assert.Equal("3-19", qb.Statistics.Single(s => s.Name == "sacks").Text);

        var interceptions = raiders.Groups.Single(g => g.Name == "Interceptions");
        Assert.Empty(interceptions.Players);
    }

    [Fact]
    public void DeserializeGameTeamStatsFixture_SectionsAsJsonElements()
    {
        var json = File.ReadAllText(FixturePath("game-team-stats.json"));
        var envelope = JsonSerializer.Deserialize<ApiSportsEnvelope<ApiSportsTeamStatsBlock>>(json, JsonOptions);

        Assert.NotNull(envelope);
        Assert.Equal("games/statistics/teams", envelope.Get);
        Assert.Equal(2, envelope.Response.Count);

        var chiefs = envelope.Response.Single(b => b.Team.Id == 17);
        Assert.True(chiefs.Statistics.ContainsKey("first_downs"));
        Assert.True(chiefs.Statistics.ContainsKey("posession"));
        Assert.Equal(24, chiefs.Statistics["first_downs"].GetProperty("total").GetInt32());
        Assert.Equal("7-12", chiefs.Statistics["first_downs"].GetProperty("third_down_efficiency").GetString());
        Assert.Equal("30:19", chiefs.Statistics["posession"].GetProperty("total").GetString());
    }

    [Fact]
    public void StatValue_Text_NormalizesNumbersAndNulls()
    {
        var json = """
        {
          "player": { "id": 5, "name": "Someone" },
          "statistics": [
            { "name": "as_string", "value": "12" },
            { "name": "as_number", "value": 12 },
            { "name": "as_decimal", "value": 7.5 },
            { "name": "as_null", "value": null }
          ]
        }
        """;
        var line = JsonSerializer.Deserialize<ApiSportsPlayerStatLine>(json, JsonOptions);

        Assert.NotNull(line);
        Assert.Equal("12", line.Statistics[0].Text);
        Assert.Equal("12", line.Statistics[1].Text);
        Assert.Equal("7.5", line.Statistics[2].Text);
        Assert.Null(line.Statistics[3].Text);
    }

    [Fact]
    public void HasErrors_TrueForNonEmptyErrorObject()
    {
        var json = """{"get":"x","parameters":{},"errors":{"token":"Invalid"},"results":0,"response":[]}""";
        var envelope = JsonSerializer.Deserialize<ApiSportsEnvelope<ApiSportsPlayer>>(json, JsonOptions)!;
        Assert.True(envelope.HasErrors);
    }

    [Fact]
    public void HasErrors_TrueForNonEmptyErrorArray()
    {
        var json = """{"get":"x","parameters":{},"errors":["quota"],"results":0,"response":[]}""";
        var envelope = JsonSerializer.Deserialize<ApiSportsEnvelope<ApiSportsPlayer>>(json, JsonOptions)!;
        Assert.True(envelope.HasErrors);
    }

    [Fact]
    public void HasErrors_FalseForEmptyErrorsArray()
    {
        var json = File.ReadAllText(FixturePath("games-by-date.json"));
        var envelope = JsonSerializer.Deserialize<ApiSportsEnvelope<ApiSportsGameItem>>(json, JsonOptions)!;
        Assert.Equal(JsonValueKind.Array, envelope.Errors.ValueKind);
        Assert.False(envelope.HasErrors);
    }
}
