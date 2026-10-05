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
