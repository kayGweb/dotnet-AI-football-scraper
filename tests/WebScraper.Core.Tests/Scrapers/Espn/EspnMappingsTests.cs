using WebScraper.Services.Scrapers.Espn;

namespace WebScraper.Tests.Scrapers.Espn;

public class EspnMappingsTests
{
    // --- ToNflAbbreviation: All 32 teams ---

    [Theory]
    [InlineData("1", "ATL")]
    [InlineData("2", "BUF")]
    [InlineData("3", "CHI")]
    [InlineData("4", "CIN")]
    [InlineData("5", "CLE")]
    [InlineData("6", "DAL")]
    [InlineData("7", "DEN")]
    [InlineData("8", "DET")]
    [InlineData("9", "GB")]
    [InlineData("10", "TEN")]
    [InlineData("11", "IND")]
    [InlineData("12", "KC")]
    [InlineData("13", "LV")]
    [InlineData("14", "LAR")]
    [InlineData("15", "MIA")]
    [InlineData("16", "MIN")]
    [InlineData("17", "NE")]
    [InlineData("18", "NO")]
    [InlineData("19", "NYG")]
    [InlineData("20", "NYJ")]
    [InlineData("21", "PHI")]
    [InlineData("22", "ARI")]
    [InlineData("23", "PIT")]
    [InlineData("24", "LAC")]
    [InlineData("25", "SF")]
    [InlineData("26", "SEA")]
    [InlineData("27", "TB")]
    [InlineData("28", "WAS")]
    [InlineData("29", "CAR")]
    [InlineData("30", "JAX")]
    [InlineData("33", "BAL")]
    [InlineData("34", "HOU")]
    public void ToNflAbbreviation_ShouldMapAllTeamsCorrectly(string espnId, string expectedAbbr)
    {
        var result = EspnMappings.ToNflAbbreviation(espnId);
        Assert.Equal(expectedAbbr, result);
    }

    [Fact]
    public void ToNflAbbreviation_ShouldMapAll32Teams()
    {
        // Verify we have exactly 32 unique NFL abbreviations mapped
        var knownIds = new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10",
            "11", "12", "13", "14", "15", "16", "17", "18", "19", "20",
            "21", "22", "23", "24", "25", "26", "27", "28", "29", "30", "33", "34" };

        var abbreviations = knownIds.Select(EspnMappings.ToNflAbbreviation).Distinct().ToList();
        Assert.Equal(32, abbreviations.Count);
    }

    [Fact]
    public void ToNflAbbreviation_UnknownId_ShouldReturnIdUnchanged()
    {
        var result = EspnMappings.ToNflAbbreviation("999");
        Assert.Equal("999", result);
    }

    // --- ToEspnId: Reverse mapping ---

    [Theory]
    [InlineData("ATL", "1")]
    [InlineData("BUF", "2")]
    [InlineData("KC", "12")]
    [InlineData("BAL", "33")]
    [InlineData("HOU", "34")]
    [InlineData("SF", "25")]
    public void ToEspnId_ShouldMapNflAbbreviationToEspnId(string nflAbbr, string expectedEspnId)
    {
        var result = EspnMappings.ToEspnId(nflAbbr);
        Assert.Equal(expectedEspnId, result);
    }

    [Fact]
    public void ToEspnId_UnknownAbbreviation_ShouldReturnNull()
    {
        var result = EspnMappings.ToEspnId("FAKE");
        Assert.Null(result);
    }

    [Fact]
    public void ToEspnId_ShouldBeCaseInsensitive()
    {
        Assert.Equal("12", EspnMappings.ToEspnId("kc"));
        Assert.Equal("12", EspnMappings.ToEspnId("KC"));
        Assert.Equal("12", EspnMappings.ToEspnId("Kc"));
    }

    // --- GetDivision: Conference/Division lookup ---

    [Theory]
    [InlineData("BUF", "AFC", "East")]
    [InlineData("MIA", "AFC", "East")]
    [InlineData("BAL", "AFC", "North")]
    [InlineData("KC", "AFC", "West")]
    [InlineData("HOU", "AFC", "South")]
    [InlineData("DAL", "NFC", "East")]
    [InlineData("CHI", "NFC", "North")]
    [InlineData("ATL", "NFC", "South")]
    [InlineData("SF", "NFC", "West")]
    public void GetDivision_ShouldReturnCorrectConferenceAndDivision(
        string nflAbbr, string expectedConference, string expectedDivision)
    {
        var (conference, division) = EspnMappings.GetDivision(nflAbbr);
        Assert.Equal(expectedConference, conference);
        Assert.Equal(expectedDivision, division);
    }

    [Fact]
    public void GetDivision_All32Teams_ShouldHaveEntries()
    {
        var allTeams = new[]
        {
            "BUF", "MIA", "NE", "NYJ",
            "BAL", "CIN", "CLE", "PIT",
            "HOU", "IND", "JAX", "TEN",
            "DEN", "KC", "LV", "LAC",
            "DAL", "NYG", "PHI", "WAS",
            "CHI", "DET", "GB", "MIN",
            "ATL", "CAR", "NO", "TB",
            "ARI", "LAR", "SF", "SEA"
        };

        foreach (var team in allTeams)
        {
            var (conference, division) = EspnMappings.GetDivision(team);
            Assert.False(string.IsNullOrEmpty(conference), $"Conference missing for {team}");
            Assert.False(string.IsNullOrEmpty(division), $"Division missing for {team}");
        }
    }

    [Fact]
    public void GetDivision_UnknownTeam_ShouldReturnEmptyStrings()
    {
        var (conference, division) = EspnMappings.GetDivision("FAKE");
        Assert.Equal("", conference);
        Assert.Equal("", division);
    }

    [Fact]
    public void GetDivision_ShouldBeCaseInsensitive()
    {
        var (conference1, division1) = EspnMappings.GetDivision("kc");
        var (conference2, division2) = EspnMappings.GetDivision("KC");
        Assert.Equal(conference1, conference2);
        Assert.Equal(division1, division2);
    }
}
