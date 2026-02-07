using WebScraper.Models;

namespace WebScraper.Tests.Models;

public class ModelTests
{
    [Fact]
    public void Team_DefaultValues_ShouldBeEmpty()
    {
        var team = new Team();

        Assert.Equal(0, team.Id);
        Assert.Equal(string.Empty, team.Name);
        Assert.Equal(string.Empty, team.Abbreviation);
        Assert.Equal(string.Empty, team.City);
        Assert.Equal(string.Empty, team.Conference);
        Assert.Equal(string.Empty, team.Division);
        Assert.Empty(team.Players);
        Assert.Empty(team.HomeGames);
        Assert.Empty(team.AwayGames);
    }

    [Fact]
    public void Player_NullableFields_ShouldDefaultToNull()
    {
        var player = new Player();

        Assert.Null(player.TeamId);
        Assert.Null(player.JerseyNumber);
        Assert.Null(player.Height);
        Assert.Null(player.Weight);
        Assert.Null(player.College);
        Assert.Null(player.Team);
    }

    [Fact]
    public void Game_Scores_ShouldBeNullable()
    {
        var game = new Game();

        Assert.Null(game.HomeScore);
        Assert.Null(game.AwayScore);
    }

    [Fact]
    public void PlayerGameStats_DefaultStats_ShouldBeZero()
    {
        var stats = new PlayerGameStats();

        Assert.Equal(0, stats.PassAttempts);
        Assert.Equal(0, stats.PassCompletions);
        Assert.Equal(0, stats.PassYards);
        Assert.Equal(0, stats.PassTouchdowns);
        Assert.Equal(0, stats.Interceptions);
        Assert.Equal(0, stats.RushAttempts);
        Assert.Equal(0, stats.RushYards);
        Assert.Equal(0, stats.RushTouchdowns);
        Assert.Equal(0, stats.Receptions);
        Assert.Equal(0, stats.ReceivingYards);
        Assert.Equal(0, stats.ReceivingTouchdowns);
    }

    [Fact]
    public void ScraperSettings_ShouldHaveSensibleDefaults()
    {
        var settings = new ScraperSettings();

        Assert.Equal(1500, settings.RequestDelayMs);
        Assert.Equal(3, settings.MaxRetries);
        Assert.Equal("NFLScraper/1.0 (educational project)", settings.UserAgent);
        Assert.Equal(30, settings.TimeoutSeconds);
    }
}
