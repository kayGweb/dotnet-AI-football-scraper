using WebScraper.Models;

namespace WebScraper.Tests.Models;

public class ScrapeResultTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var result = new ScrapeResult();

        Assert.False(result.Success);
        Assert.Equal(0, result.RecordsProcessed);
        Assert.Equal(0, result.RecordsFailed);
        Assert.Equal(string.Empty, result.Message);
        Assert.NotNull(result.Errors);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Succeeded_ShouldSetSuccessAndCount()
    {
        var result = ScrapeResult.Succeeded(5, "5 teams processed");

        Assert.True(result.Success);
        Assert.Equal(5, result.RecordsProcessed);
        Assert.Equal("5 teams processed", result.Message);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Failed_WithMessage_ShouldSetFailureState()
    {
        var result = ScrapeResult.Failed("Something went wrong");

        Assert.False(result.Success);
        Assert.Equal(0, result.RecordsProcessed);
        Assert.Equal("Something went wrong", result.Message);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Failed_WithErrors_ShouldIncludeErrorList()
    {
        var errors = new List<string> { "Error 1", "Error 2" };
        var result = ScrapeResult.Failed("Multiple failures", errors);

        Assert.False(result.Success);
        Assert.Equal("Multiple failures", result.Message);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains("Error 1", result.Errors);
        Assert.Contains("Error 2", result.Errors);
    }

    [Fact]
    public void Succeeded_WithZeroRecords_ShouldStillBeSuccess()
    {
        var result = ScrapeResult.Succeeded(0, "No records found");

        Assert.True(result.Success);
        Assert.Equal(0, result.RecordsProcessed);
    }
}
