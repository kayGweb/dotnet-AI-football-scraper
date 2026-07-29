using WebScraper.Models;
using WebScraper.Services;
using WebScraper.Services.Coverage;

namespace WebScraper.Tests.Services;

public class BackfillPlannerTests
{
    [Fact]
    public void Plan_SingleSeason_IncludesAllSeasonTypes()
    {
        var items = BackfillPlanner.Plan(2025, 2025);

        Assert.Contains(items, i => i.SeasonType == NflSeasonType.Preseason && i.JobType == ScrapeJobType.Games);
        Assert.Contains(items, i => i.SeasonType == NflSeasonType.Regular && i.JobType == ScrapeJobType.Stats);
        Assert.Contains(items, i => i.SeasonType == NflSeasonType.Postseason);
    }

    [Fact]
    public void Plan_OrdersReverseChronological()
    {
        var items = BackfillPlanner.Plan(2024, 2025).ToList();

        Assert.Equal(2025, items[0].Season);
        var idx2025 = items.FindIndex(i => i.Season == 2025);
        var idx2024 = items.FindIndex(i => i.Season == 2024);
        Assert.True(idx2024 > idx2025);
    }
}

public class FranchiseMappingsTests
{
    [Theory]
    [InlineData("STL", "LAR")]
    [InlineData("SD", "LAC")]
    [InlineData("OAK", "LV")]
    [InlineData("KC", "KC")]
    public void ToCanonicalAbbreviation_MapsRelocations(string input, string expected)
    {
        Assert.Equal(expected, FranchiseMappings.ToCanonicalAbbreviation(input));
    }
}
