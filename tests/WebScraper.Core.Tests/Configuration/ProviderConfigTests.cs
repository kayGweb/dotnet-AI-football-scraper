using Microsoft.Extensions.Configuration;
using WebScraper.Models;

namespace WebScraper.Tests.Configuration;

public class ProviderConfigTests
{
    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    [Fact]
    public void ScraperSettings_ShouldBindFromConfiguration()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ScraperSettings:RequestDelayMs"] = "2000",
            ["ScraperSettings:MaxRetries"] = "5",
            ["ScraperSettings:UserAgent"] = "CustomAgent/2.0",
            ["ScraperSettings:TimeoutSeconds"] = "60",
            ["ScraperSettings:DataProvider"] = "Espn"
        });

        var settings = new ScraperSettings();
        config.GetSection("ScraperSettings").Bind(settings);

        Assert.Equal(2000, settings.RequestDelayMs);
        Assert.Equal(5, settings.MaxRetries);
        Assert.Equal("CustomAgent/2.0", settings.UserAgent);
        Assert.Equal(60, settings.TimeoutSeconds);
        Assert.Equal("Espn", settings.DataProvider);
    }

    [Fact]
    public void ScraperSettings_DefaultValues_ShouldApplyWhenNotConfigured()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>());

        var settings = new ScraperSettings();
        config.GetSection("ScraperSettings").Bind(settings);

        Assert.Equal(1500, settings.RequestDelayMs);
        Assert.Equal(3, settings.MaxRetries);
        Assert.Equal("ProFootballReference", settings.DataProvider);
    }

    [Fact]
    public void ApiProviderSettings_ShouldBindFromConfiguration()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ScraperSettings:Providers:Espn:BaseUrl"] = "https://espn.api.test",
            ["ScraperSettings:Providers:Espn:AuthType"] = "None",
            ["ScraperSettings:Providers:Espn:RequestDelayMs"] = "500"
        });

        var settings = new ScraperSettings();
        config.GetSection("ScraperSettings").Bind(settings);

        Assert.True(settings.Providers.ContainsKey("Espn"));
        var espn = settings.Providers["Espn"];
        Assert.Equal("https://espn.api.test", espn.BaseUrl);
        Assert.Equal("None", espn.AuthType);
        Assert.Equal(500, espn.RequestDelayMs);
    }

    [Fact]
    public void ApiProviderSettings_SportsDataIo_ShouldBindWithApiKey()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ScraperSettings:Providers:SportsDataIo:BaseUrl"] = "https://sportsdata.test",
            ["ScraperSettings:Providers:SportsDataIo:ApiKey"] = "test-key-123",
            ["ScraperSettings:Providers:SportsDataIo:AuthType"] = "Header",
            ["ScraperSettings:Providers:SportsDataIo:AuthHeaderName"] = "Ocp-Apim-Subscription-Key"
        });

        var settings = new ScraperSettings();
        config.GetSection("ScraperSettings").Bind(settings);

        var sportsData = settings.Providers["SportsDataIo"];
        Assert.Equal("test-key-123", sportsData.ApiKey);
        Assert.Equal("Header", sportsData.AuthType);
        Assert.Equal("Ocp-Apim-Subscription-Key", sportsData.AuthHeaderName);
    }

    [Fact]
    public void ApiProviderSettings_MySportsFeeds_ShouldBindWithBasicAuth()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ScraperSettings:Providers:MySportsFeeds:BaseUrl"] = "https://msf.test",
            ["ScraperSettings:Providers:MySportsFeeds:ApiKey"] = "msf-key",
            ["ScraperSettings:Providers:MySportsFeeds:AuthType"] = "Basic"
        });

        var settings = new ScraperSettings();
        config.GetSection("ScraperSettings").Bind(settings);

        var msf = settings.Providers["MySportsFeeds"];
        Assert.Equal("msf-key", msf.ApiKey);
        Assert.Equal("Basic", msf.AuthType);
    }

    [Fact]
    public void ApiProviderSettings_MissingApiKey_ShouldDefaultToNull()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ScraperSettings:Providers:SportsDataIo:BaseUrl"] = "https://sportsdata.test",
            ["ScraperSettings:Providers:SportsDataIo:AuthType"] = "Header",
            ["ScraperSettings:Providers:SportsDataIo:AuthHeaderName"] = "Ocp-Apim-Subscription-Key"
        });

        var settings = new ScraperSettings();
        config.GetSection("ScraperSettings").Bind(settings);

        Assert.Null(settings.Providers["SportsDataIo"].ApiKey);
    }

    [Fact]
    public void ApiProviderSettings_DefaultValues_ShouldApply()
    {
        var settings = new ApiProviderSettings();

        Assert.Equal(string.Empty, settings.BaseUrl);
        Assert.Null(settings.ApiKey);
        Assert.Equal("None", settings.AuthType);
        Assert.Null(settings.AuthHeaderName);
        Assert.Equal(1000, settings.RequestDelayMs);
        Assert.Empty(settings.CustomHeaders);
    }

    [Fact]
    public void DataProviderOverride_ViaInMemoryCollection_ShouldWork()
    {
        // Simulates the --source CLI flag override
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ScraperSettings:DataProvider"] = "ProFootballReference"
        });

        // Override with in-memory collection (like --source flag does)
        var overrideConfig = new ConfigurationBuilder()
            .AddConfiguration(config)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ScraperSettings:DataProvider"] = "Espn"
            })
            .Build();

        var settings = new ScraperSettings();
        overrideConfig.GetSection("ScraperSettings").Bind(settings);

        Assert.Equal("Espn", settings.DataProvider);
    }

    [Fact]
    public void MultipleProviders_ShouldBindToProvidersDictionary()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ScraperSettings:Providers:Espn:BaseUrl"] = "https://espn.test",
            ["ScraperSettings:Providers:Espn:AuthType"] = "None",
            ["ScraperSettings:Providers:SportsDataIo:BaseUrl"] = "https://sportsdata.test",
            ["ScraperSettings:Providers:SportsDataIo:AuthType"] = "Header",
            ["ScraperSettings:Providers:MySportsFeeds:BaseUrl"] = "https://msf.test",
            ["ScraperSettings:Providers:MySportsFeeds:AuthType"] = "Basic",
            ["ScraperSettings:Providers:NflCom:BaseUrl"] = "https://nfl.test",
            ["ScraperSettings:Providers:NflCom:AuthType"] = "None"
        });

        var settings = new ScraperSettings();
        config.GetSection("ScraperSettings").Bind(settings);

        Assert.Equal(4, settings.Providers.Count);
        Assert.True(settings.Providers.ContainsKey("Espn"));
        Assert.True(settings.Providers.ContainsKey("SportsDataIo"));
        Assert.True(settings.Providers.ContainsKey("MySportsFeeds"));
        Assert.True(settings.Providers.ContainsKey("NflCom"));
    }
}
