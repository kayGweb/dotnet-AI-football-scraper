using WebScraper.Services.Scrapers;

namespace WebScraper.Tests.Scrapers;

public class GameScraperParsingTests
{
    [Theory]
    [InlineData("crd", "ARI")]
    [InlineData("rav", "BAL")]
    [InlineData("gnb", "GB")]
    [InlineData("kan", "KC")]
    [InlineData("sdg", "LAC")]
    [InlineData("ram", "LAR")]
    [InlineData("rai", "LV")]
    [InlineData("nwe", "NE")]
    [InlineData("nor", "NO")]
    [InlineData("sfo", "SF")]
    [InlineData("tam", "TB")]
    [InlineData("oti", "TEN")]
    [InlineData("htx", "HOU")]
    [InlineData("clt", "IND")]
    public void PfrToNflAbbreviation_ShouldMapCorrectly(string pfr, string expected)
    {
        // PfrToNflAbbreviation is internal static on GameScraperService
        var method = typeof(GameScraperService).GetMethod("PfrToNflAbbreviation",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public);
        Assert.NotNull(method);

        var result = method.Invoke(null, new object[] { pfr }) as string;
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("buf", "BUF")]
    [InlineData("dal", "DAL")]
    [InlineData("pit", "PIT")]
    [InlineData("sea", "SEA")]
    public void PfrToNflAbbreviation_ShouldUppercaseUnmappedAbbreviations(string pfr, string expected)
    {
        var method = typeof(GameScraperService).GetMethod("PfrToNflAbbreviation",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public);
        Assert.NotNull(method);

        var result = method!.Invoke(null, new object[] { pfr }) as string;
        Assert.Equal(expected, result);
    }
}
