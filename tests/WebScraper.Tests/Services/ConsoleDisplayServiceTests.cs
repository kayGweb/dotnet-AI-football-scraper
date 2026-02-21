using WebScraper.Models;
using WebScraper.Services;

namespace WebScraper.Tests.Services;

public class ConsoleDisplayServiceTests
{
    private readonly ConsoleDisplayService _display = new();

    private string CaptureConsoleOutput(Action action)
    {
        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            action();
            return sw.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void PrintBanner_ShouldIncludeTitle()
    {
        var output = CaptureConsoleOutput(() =>
            _display.PrintBanner("Espn", "Sqlite", "Data Source=test.db"));

        Assert.Contains("NFL Web Scraper v1.0", output);
        Assert.Contains("ESPN API", output);
        Assert.Contains("SQLite", output);
    }

    [Fact]
    public void PrintScrapeResult_Success_ShouldShowOk()
    {
        var result = ScrapeResult.Succeeded(32, "32 teams processed");
        var output = CaptureConsoleOutput(() =>
            _display.PrintScrapeResult("Teams", result));

        Assert.Contains("[OK]", output);
        Assert.Contains("32 teams processed", output);
    }

    [Fact]
    public void PrintScrapeResult_Failure_ShouldShowFail()
    {
        var result = ScrapeResult.Failed("Network error", new List<string> { "Timeout" });
        var output = CaptureConsoleOutput(() =>
            _display.PrintScrapeResult("Teams", result));

        Assert.Contains("[FAIL]", output);
        Assert.Contains("Network error", output);
        Assert.Contains("Timeout", output);
    }

    [Fact]
    public void PrintTeamsTable_ShouldShowTeamData()
    {
        var teams = new List<Team>
        {
            new() { Abbreviation = "KC", Name = "Kansas City Chiefs", City = "Kansas City", Conference = "AFC", Division = "West" },
            new() { Abbreviation = "BUF", Name = "Buffalo Bills", City = "Buffalo", Conference = "AFC", Division = "East" }
        };

        var output = CaptureConsoleOutput(() => _display.PrintTeamsTable(teams));

        Assert.Contains("KC", output);
        Assert.Contains("Kansas City Chiefs", output);
        Assert.Contains("BUF", output);
        Assert.Contains("2 teams total", output);
    }

    [Fact]
    public void PrintTeamsTable_EmptyList_ShouldShowWarning()
    {
        var output = CaptureConsoleOutput(() => _display.PrintTeamsTable(new List<Team>()));

        Assert.Contains("No teams found", output);
    }

    [Fact]
    public void PrintPlayersTable_ShouldShowPlayerData()
    {
        var players = new List<Player>
        {
            new() { Name = "Patrick Mahomes", Position = "QB", JerseyNumber = 15, Height = "6-3", Weight = 230, College = "Texas Tech" }
        };

        var output = CaptureConsoleOutput(() => _display.PrintPlayersTable(players));

        Assert.Contains("Patrick Mahomes", output);
        Assert.Contains("QB", output);
        Assert.Contains("15", output);
        Assert.Contains("1 players total", output);
    }

    [Fact]
    public void PrintPlayersTable_WithTeamName_ShouldShowRosterHeader()
    {
        var players = new List<Player>
        {
            new() { Name = "Patrick Mahomes", Position = "QB" }
        };

        var output = CaptureConsoleOutput(() => _display.PrintPlayersTable(players, "Kansas City Chiefs"));

        Assert.Contains("Kansas City Chiefs", output);
    }

    [Fact]
    public void PrintGamesTable_ShouldShowGameData()
    {
        var homeTeam = new Team { Abbreviation = "KC" };
        var awayTeam = new Team { Abbreviation = "BUF" };
        var games = new List<Game>
        {
            new()
            {
                Season = 2025, Week = 1, HomeScore = 27, AwayScore = 24,
                HomeTeam = homeTeam, AwayTeam = awayTeam,
                GameDate = new DateTime(2025, 9, 7)
            }
        };

        var output = CaptureConsoleOutput(() => _display.PrintGamesTable(games, 2025, 1));

        Assert.Contains("KC", output);
        Assert.Contains("BUF", output);
        Assert.Contains("27", output);
        Assert.Contains("24", output);
        Assert.Contains("1 games total", output);
    }

    [Fact]
    public void PrintStatsTable_ShouldShowStatLines()
    {
        var stats = new List<PlayerGameStats>
        {
            new()
            {
                Player = new Player { Name = "Patrick Mahomes" },
                PassCompletions = 25, PassAttempts = 35, PassYards = 300,
                PassTouchdowns = 3, Interceptions = 1
            }
        };

        var output = CaptureConsoleOutput(() => _display.PrintStatsTable(stats));

        Assert.Contains("Patrick Mahomes", output);
        Assert.Contains("25/35", output);
        Assert.Contains("300", output);
        Assert.Contains("1 stat lines total", output);
    }

    [Fact]
    public void PrintDatabaseStatus_ShouldShowCounts()
    {
        var output = CaptureConsoleOutput(() =>
            _display.PrintDatabaseStatus(32, 1696, 272, 4352));

        Assert.Contains("Database Status", output);
        Assert.Contains("32", output);
        Assert.Contains("1,696", output);
        Assert.Contains("272", output);
        Assert.Contains("4,352", output);
    }

    [Fact]
    public void PrintError_ShouldShowErrorTag()
    {
        var output = CaptureConsoleOutput(() => _display.PrintError("Something failed"));

        Assert.Contains("[ERROR]", output);
        Assert.Contains("Something failed", output);
    }

    [Fact]
    public void PrintSuccess_ShouldShowOkTag()
    {
        var output = CaptureConsoleOutput(() => _display.PrintSuccess("All good"));

        Assert.Contains("[OK]", output);
        Assert.Contains("All good", output);
    }

    [Fact]
    public void PrintWarning_ShouldShowWarnTag()
    {
        var output = CaptureConsoleOutput(() => _display.PrintWarning("Watch out"));

        Assert.Contains("[WARN]", output);
        Assert.Contains("Watch out", output);
    }

    [Fact]
    public void PrintMainMenu_ShouldShowAllOptions()
    {
        var output = CaptureConsoleOutput(() => _display.PrintMainMenu("Espn"));

        Assert.Contains("Main Menu", output);
        Assert.Contains("Scrape data", output);
        Assert.Contains("View data", output);
        Assert.Contains("Database status", output);
        Assert.Contains("Change source", output);
        Assert.Contains("ESPN API", output);
        Assert.Contains("Exit", output);
    }

    [Fact]
    public void PrintScrapeMenu_ShouldShowAllOptions()
    {
        var output = CaptureConsoleOutput(() => _display.PrintScrapeMenu());

        Assert.Contains("Scrape Menu", output);
        Assert.Contains("Teams (all 32)", output);
        Assert.Contains("Single team", output);
        Assert.Contains("Full pipeline", output);
        Assert.Contains("Back to main menu", output);
    }

    [Fact]
    public void PrintViewMenu_ShouldShowAllOptions()
    {
        var output = CaptureConsoleOutput(() => _display.PrintViewMenu());

        Assert.Contains("View Menu", output);
        Assert.Contains("Teams", output);
        Assert.Contains("Players (by team)", output);
        Assert.Contains("Games (by season/week)", output);
        Assert.Contains("Player stats", output);
    }

    [Fact]
    public void PrintSourceMenu_ShouldShowAllProviders()
    {
        var output = CaptureConsoleOutput(() => _display.PrintSourceMenu("ProFootballReference"));

        Assert.Contains("Change Data Source", output);
        Assert.Contains("Pro Football Reference", output);
        Assert.Contains("ESPN", output);
        Assert.Contains("SportsData.io", output);
        Assert.Contains("MySportsFeeds", output);
        Assert.Contains("NFL.com", output);
        Assert.Contains("Cancel", output);
    }

    [Fact]
    public void IsValidProvider_ShouldAcceptValidProviders()
    {
        Assert.True(ConsoleDisplayService.IsValidProvider("Espn"));
        Assert.True(ConsoleDisplayService.IsValidProvider("espn"));
        Assert.True(ConsoleDisplayService.IsValidProvider("ProFootballReference"));
        Assert.True(ConsoleDisplayService.IsValidProvider("SportsDataIo"));
        Assert.True(ConsoleDisplayService.IsValidProvider("MySportsFeeds"));
        Assert.True(ConsoleDisplayService.IsValidProvider("NflCom"));
    }

    [Fact]
    public void IsValidProvider_ShouldRejectInvalidProviders()
    {
        Assert.False(ConsoleDisplayService.IsValidProvider("Invalid"));
        Assert.False(ConsoleDisplayService.IsValidProvider(""));
        Assert.False(ConsoleDisplayService.IsValidProvider("ESPN_API"));
    }

    [Fact]
    public void GetProviderDisplayName_ShouldReturnHumanReadableNames()
    {
        Assert.Equal("Pro Football Reference (HTML)", ConsoleDisplayService.GetProviderDisplayName("ProFootballReference"));
        Assert.Equal("ESPN API", ConsoleDisplayService.GetProviderDisplayName("Espn"));
        Assert.Equal("SportsData.io API", ConsoleDisplayService.GetProviderDisplayName("SportsDataIo"));
        Assert.Equal("MySportsFeeds API", ConsoleDisplayService.GetProviderDisplayName("MySportsFeeds"));
        Assert.Equal("NFL.com API", ConsoleDisplayService.GetProviderDisplayName("NflCom"));
    }

    [Fact]
    public void GetProviderDisplayName_UnknownProvider_ShouldReturnAsIs()
    {
        Assert.Equal("SomeProvider", ConsoleDisplayService.GetProviderDisplayName("SomeProvider"));
    }
}
