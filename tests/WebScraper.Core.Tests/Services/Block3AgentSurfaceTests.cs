using WebScraper.Services.Agent;
using WebScraper.Tests.Helpers;

namespace WebScraper.Tests.Services;

public class Block3AgentSurfaceTests
{
    [Fact]
    public void DescribeSchema_ReturnsCatalogWhenNoEntity()
    {
        var result = SchemaDescriptionService.Describe(null);
        Assert.NotNull(result);
    }

    [Fact]
    public void DescribeSchema_ReturnsGameEntity()
    {
        var result = SchemaDescriptionService.Describe("Game");
        Assert.Contains("Game", result.ToString());
    }

    [Fact]
    public void DataDictionary_HasPassYards()
    {
        var dict = DataDictionaryService.GetDictionary();
        var json = System.Text.Json.JsonSerializer.Serialize(dict);
        Assert.Contains("PassYards", json);
    }

    [Fact]
    public async Task DataCorrectionService_ProposeAndApprove()
    {
        var db = TestDbContextFactory.Create();
        var svc = new DataCorrectionService(db);

        var team = new WebScraper.Models.Team
        {
            Name = "Test Team",
            Abbreviation = "TST",
            City = "Test",
            Conference = "AFC",
            Division = "East",
        };
        db.Teams.Add(team);
        await db.SaveChangesAsync();

        var proposal = await svc.ProposeAsync("Team", team.Id, "City", "New City", "Fix typo", "test-agent");
        Assert.Equal(WebScraper.Models.DataCorrectionStatus.Pending, proposal.Status);

        var approved = await svc.ApproveAsync(proposal.Id, "admin");
        Assert.NotNull(approved);
        Assert.Equal(WebScraper.Models.DataCorrectionStatus.Applied, approved!.Status);

        var updated = await db.Teams.FindAsync(team.Id);
        Assert.Equal("New City", updated!.City);
    }
}
