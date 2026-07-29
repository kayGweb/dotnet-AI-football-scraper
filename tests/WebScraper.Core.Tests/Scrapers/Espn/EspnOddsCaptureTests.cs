using WebScraper.Models;
using WebScraper.Services.Scrapers.Espn;

namespace WebScraper.Tests.Scrapers.Espn;

public class EspnOddsCaptureTests
{
    [Theory]
    [InlineData(false, false, false, OddsSnapshotType.Opening)]
    [InlineData(false, true, false, OddsSnapshotType.Current)]
    [InlineData(true, false, false, OddsSnapshotType.Closing)]
    [InlineData(true, true, false, OddsSnapshotType.Closing)]
    [InlineData(true, true, true, null)]
    public void ResolveSnapshotType_ReturnsExpected(
        bool isFinal, bool hasOpening, bool hasClosing, OddsSnapshotType? expected)
    {
        var result = EspnOddsCapture.ResolveSnapshotType(isFinal, hasOpening, hasClosing);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("STATUS_FINAL", 24, 17, true)]
    [InlineData("STATUS_SCHEDULED", null, null, false)]
    [InlineData("STATUS_IN_PROGRESS", 10, 7, false)]
    [InlineData(null, 21, 14, true)]
    public void IsGameFinal_DetectsCompletedGames(string? status, int? home, int? away, bool expected)
    {
        var game = new Game
        {
            GameStatus = status,
            HomeScore = home,
            AwayScore = away,
        };

        Assert.Equal(expected, EspnOddsCapture.IsGameFinal(game));
    }
}
