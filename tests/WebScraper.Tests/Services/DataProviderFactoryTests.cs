using Microsoft.Extensions.DependencyInjection;
using WebScraper.Models;
using WebScraper.Services;
using WebScraper.Services.Scrapers;
using WebScraper.Services.Scrapers.Espn;
using WebScraper.Services.Scrapers.MySportsFeeds;
using WebScraper.Services.Scrapers.NflCom;
using WebScraper.Services.Scrapers.SportsDataIo;

namespace WebScraper.Tests.Services;

public class DataProviderFactoryTests
{
    private static ScraperSettings CreateSettings(string provider)
    {
        return new ScraperSettings
        {
            DataProvider = provider,
            RequestDelayMs = 0,
            MaxRetries = 1,
            UserAgent = "Test/1.0",
            TimeoutSeconds = 5,
            Providers = new Dictionary<string, ApiProviderSettings>
            {
                ["Espn"] = new() { BaseUrl = "http://espn.test", AuthType = "None" },
                ["SportsDataIo"] = new() { BaseUrl = "http://sportsdata.test", AuthType = "Header", ApiKey = "key", AuthHeaderName = "X-Key" },
                ["MySportsFeeds"] = new() { BaseUrl = "http://msf.test", AuthType = "Basic", ApiKey = "key" },
                ["NflCom"] = new() { BaseUrl = "http://nfl.test", AuthType = "None" }
            }
        };
    }

    [Fact]
    public void RegisterScrapers_ProFootballReference_ShouldRegisterHtmlScrapers()
    {
        var services = new ServiceCollection();
        var settings = CreateSettings("ProFootballReference");

        DataProviderFactory.RegisterScrapers(services, settings);

        // Verify all 4 scraper interfaces are registered
        Assert.Contains(services, sd => sd.ServiceType == typeof(ITeamScraperService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IPlayerScraperService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IGameScraperService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IStatsScraperService));
    }

    [Fact]
    public void RegisterScrapers_Espn_ShouldRegisterEspnServices()
    {
        var services = new ServiceCollection();
        var settings = CreateSettings("Espn");

        DataProviderFactory.RegisterScrapers(services, settings);

        Assert.Contains(services, sd => sd.ServiceType == typeof(ITeamScraperService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IPlayerScraperService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IGameScraperService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IStatsScraperService));
    }

    [Fact]
    public void RegisterScrapers_SportsDataIo_ShouldRegisterSportsDataServices()
    {
        var services = new ServiceCollection();
        var settings = CreateSettings("SportsDataIo");

        DataProviderFactory.RegisterScrapers(services, settings);

        Assert.Contains(services, sd => sd.ServiceType == typeof(ITeamScraperService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IPlayerScraperService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IGameScraperService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IStatsScraperService));
    }

    [Fact]
    public void RegisterScrapers_MySportsFeeds_ShouldRegisterMySportsFeedsServices()
    {
        var services = new ServiceCollection();
        var settings = CreateSettings("MySportsFeeds");

        DataProviderFactory.RegisterScrapers(services, settings);

        Assert.Contains(services, sd => sd.ServiceType == typeof(ITeamScraperService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IPlayerScraperService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IGameScraperService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IStatsScraperService));
    }

    [Fact]
    public void RegisterScrapers_NflCom_ShouldRegisterNflComServices()
    {
        var services = new ServiceCollection();
        var settings = CreateSettings("NflCom");

        DataProviderFactory.RegisterScrapers(services, settings);

        Assert.Contains(services, sd => sd.ServiceType == typeof(ITeamScraperService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IPlayerScraperService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IGameScraperService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IStatsScraperService));
    }

    [Fact]
    public void RegisterScrapers_InvalidProvider_ShouldThrowInvalidOperationException()
    {
        var services = new ServiceCollection();
        var settings = CreateSettings("InvalidProvider");

        var ex = Assert.Throws<InvalidOperationException>(
            () => DataProviderFactory.RegisterScrapers(services, settings));

        Assert.Contains("Unsupported data provider", ex.Message);
        Assert.Contains("InvalidProvider", ex.Message);
    }

    [Theory]
    [InlineData("profootballreference")]
    [InlineData("PROFOOTBALLREFERENCE")]
    [InlineData("ProFootballReference")]
    public void RegisterScrapers_ShouldBeCaseInsensitive(string provider)
    {
        var services = new ServiceCollection();
        var settings = CreateSettings(provider);

        DataProviderFactory.RegisterScrapers(services, settings);

        Assert.Contains(services, sd => sd.ServiceType == typeof(ITeamScraperService));
    }

    [Theory]
    [InlineData("espn")]
    [InlineData("ESPN")]
    [InlineData("Espn")]
    public void RegisterScrapers_EspnProvider_ShouldBeCaseInsensitive(string provider)
    {
        var services = new ServiceCollection();
        var settings = CreateSettings(provider);

        DataProviderFactory.RegisterScrapers(services, settings);

        Assert.Contains(services, sd => sd.ServiceType == typeof(ITeamScraperService));
    }
}
