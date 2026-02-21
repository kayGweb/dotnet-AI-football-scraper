using Serilog;
using WebScraper.Models;

namespace WebScraper.Services;

public class ConsoleDisplayService
{
    private static readonly Serilog.ILogger FileLogger = Log.ForContext<ConsoleDisplayService>();

    private static readonly string[] ValidProviders =
        ["ProFootballReference", "Espn", "SportsDataIo", "MySportsFeeds", "NflCom"];

    public void PrintAbbreviationsTable()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  NFL Team Abbreviations");
        Console.ResetColor();
        Console.WriteLine("  " + new string('-', 40));

        foreach (var group in NflTeams.ByDivision())
        {
            var abbrs = string.Join("  ", group.Select(t => $"{t.Abbreviation,-4}"));
            Console.WriteLine($"  {group.Key + ":",-12} {abbrs}");
        }

        Console.WriteLine();
    }

    public void PrintBanner(string provider, string dbProvider, string connectionString)
    {
        var sourceName = GetProviderDisplayName(provider);
        var dbInfo = FormatDbInfo(dbProvider, connectionString);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  NFL Web Scraper v1.0");
        Console.ResetColor();
        Console.WriteLine($"  Source: {sourceName}  |  Database: {dbInfo}");
        Console.WriteLine("  " + new string('-', 52));
        Console.WriteLine();
    }

    public void PrintScrapeResult(string operation, ScrapeResult result)
    {
        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  [OK] ");
            Console.ResetColor();
            Console.WriteLine($"{operation}: {result.Message}");

            FileLogger.Information("Scrape {Operation} succeeded: {Message} ({RecordsProcessed} records)",
                operation, result.Message, result.RecordsProcessed);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("  [FAIL] ");
            Console.ResetColor();
            Console.WriteLine($"{operation}: {result.Message}");

            foreach (var error in result.Errors)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"         - {error}");
                Console.ResetColor();
            }

            FileLogger.Error("Scrape {Operation} failed: {Message}. Errors: {Errors}",
                operation, result.Message, result.Errors);
        }
    }

    public void PrintProgress(string operation, int current, int total)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"\r  {operation}: {current}/{total}...");
        Console.ResetColor();

        if (current == total)
            Console.WriteLine();
    }

    public void PrintTeamsTable(IEnumerable<Team> teams)
    {
        var teamsList = teams.ToList();
        if (teamsList.Count == 0)
        {
            PrintWarning("No teams found in database.");
            return;
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  {"Abbr",-6} {"Name",-28} {"City",-18} {"Conf",-5} {"Division",-8}");
        Console.ResetColor();
        Console.WriteLine("  " + new string('-', 67));

        foreach (var team in teamsList.OrderBy(t => t.Conference).ThenBy(t => t.Division).ThenBy(t => t.Name))
        {
            Console.WriteLine($"  {team.Abbreviation,-6} {team.Name,-28} {team.City,-18} {team.Conference,-5} {team.Division,-8}");
        }

        Console.WriteLine();
        Console.WriteLine($"  {teamsList.Count} teams total");
        Console.WriteLine();
    }

    public void PrintPlayersTable(IEnumerable<Player> players, string? teamName = null)
    {
        var playersList = players.ToList();
        if (playersList.Count == 0)
        {
            PrintWarning(teamName != null ? $"No players found for {teamName}." : "No players found in database.");
            return;
        }

        var header = teamName != null ? $"Roster: {teamName}" : "Players";
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  {header}");
        Console.WriteLine($"  {"#",-4} {"Name",-26} {"Pos",-5} {"Height",-8} {"Weight",-7} {"College",-20}");
        Console.ResetColor();
        Console.WriteLine("  " + new string('-', 72));

        foreach (var player in playersList.OrderBy(p => p.Position).ThenBy(p => p.Name))
        {
            var jersey = player.JerseyNumber?.ToString() ?? "-";
            var height = player.Height ?? "-";
            var weight = player.Weight?.ToString() ?? "-";
            var college = player.College ?? "-";
            Console.WriteLine($"  {jersey,-4} {player.Name,-26} {player.Position,-5} {height,-8} {weight,-7} {college,-20}");
        }

        Console.WriteLine();
        Console.WriteLine($"  {playersList.Count} players total");
        Console.WriteLine();
    }

    public void PrintGamesTable(IEnumerable<Game> games, int? season = null, int? week = null)
    {
        var gamesList = games.ToList();
        if (gamesList.Count == 0)
        {
            var context = season != null ? $" for season {season}" + (week != null ? $" week {week}" : "") : "";
            PrintWarning($"No games found{context}.");
            return;
        }

        var header = season != null ? $"Games: {season} Season" + (week != null ? $" Week {week}" : "") : "Games";
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  {header}");
        Console.WriteLine($"  {"Wk",-4} {"Date",-12} {"Away",-6} {"Score",-11} {"Home",-6}");
        Console.ResetColor();
        Console.WriteLine("  " + new string('-', 41));

        foreach (var game in gamesList.OrderBy(g => g.Week).ThenBy(g => g.GameDate))
        {
            var date = game.GameDate != DateTime.MinValue ? game.GameDate.ToString("MM/dd/yyyy") : "-";
            var awayAbbr = game.AwayTeam?.Abbreviation ?? "???";
            var homeAbbr = game.HomeTeam?.Abbreviation ?? "???";
            var score = game.AwayScore != null && game.HomeScore != null
                ? $"{game.AwayScore,3} - {game.HomeScore,-3}"
                : "   -   ";
            Console.WriteLine($"  {game.Week,-4} {date,-12} {awayAbbr,-6} {score,-11} {homeAbbr,-6}");
        }

        Console.WriteLine();
        Console.WriteLine($"  {gamesList.Count} games total");
        Console.WriteLine();
    }

    public void PrintStatsTable(IEnumerable<PlayerGameStats> stats)
    {
        var statsList = stats.ToList();
        if (statsList.Count == 0)
        {
            PrintWarning("No stats found.");
            return;
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  {"Player",-22} {"C/A",-8} {"PYd",-6} {"PTD",-4} {"INT",-4} {"RAtt",-5} {"RYd",-6} {"RTD",-4} {"Rec",-4} {"RecYd",-6} {"RecTD",-5}");
        Console.ResetColor();
        Console.WriteLine("  " + new string('-', 78));

        foreach (var s in statsList.OrderByDescending(s => s.PassYards + s.RushYards + s.ReceivingYards))
        {
            var playerName = s.Player?.Name ?? $"Player#{s.PlayerId}";
            if (playerName.Length > 21) playerName = playerName[..21];
            var compAtt = $"{s.PassCompletions}/{s.PassAttempts}";
            Console.WriteLine($"  {playerName,-22} {compAtt,-8} {s.PassYards,-6} {s.PassTouchdowns,-4} {s.Interceptions,-4} {s.RushAttempts,-5} {s.RushYards,-6} {s.RushTouchdowns,-4} {s.Receptions,-4} {s.ReceivingYards,-6} {s.ReceivingTouchdowns,-5}");
        }

        Console.WriteLine();
        Console.WriteLine($"  {statsList.Count} stat lines total");
        Console.WriteLine();
    }

    public void PrintDatabaseStatus(int teams, int players, int games, int stats)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  Database Status");
        Console.ResetColor();
        Console.WriteLine("  " + new string('-', 30));
        Console.WriteLine($"  Teams:        {teams,8:N0}");
        Console.WriteLine($"  Players:      {players,8:N0}");
        Console.WriteLine($"  Games:        {games,8:N0}");
        Console.WriteLine($"  Stat Lines:   {stats,8:N0}");
        Console.WriteLine();
    }

    public void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("  [ERROR] ");
        Console.ResetColor();
        Console.WriteLine(message);

        FileLogger.Error("{Message}", message);
    }

    public void PrintWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("  [WARN] ");
        Console.ResetColor();
        Console.WriteLine(message);

        FileLogger.Warning("{Message}", message);
    }

    public void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("  [OK] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public void PrintInfo(string message)
    {
        Console.WriteLine($"  {message}");
    }

    public void PrintMainMenu(string currentSource)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  Main Menu");
        Console.ResetColor();
        Console.WriteLine("  " + new string('-', 40));
        Console.WriteLine("  1. Scrape data");
        Console.WriteLine("  2. View data");
        Console.WriteLine("  3. Database status");
        Console.WriteLine($"  4. Change source (current: {GetProviderDisplayName(currentSource)})");
        Console.WriteLine("  5. Exit");
        Console.WriteLine();
    }

    public void PrintScrapeMenu()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  Scrape Menu");
        Console.ResetColor();
        Console.WriteLine("  " + new string('-', 40));
        Console.WriteLine("  1. Teams (all 32)");
        Console.WriteLine("  2. Single team");
        Console.WriteLine("  3. Players (all rosters)");
        Console.WriteLine("  4. Games (full season)");
        Console.WriteLine("  5. Games (single week)");
        Console.WriteLine("  6. Player stats (single week)");
        Console.WriteLine("  7. Full pipeline (teams + players + games)");
        Console.WriteLine("  8. Back to main menu");
        Console.WriteLine();
    }

    public void PrintViewMenu()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  View Menu");
        Console.ResetColor();
        Console.WriteLine("  " + new string('-', 40));
        Console.WriteLine("  1. Teams");
        Console.WriteLine("  2. Players (by team)");
        Console.WriteLine("  3. Games (by season/week)");
        Console.WriteLine("  4. Player stats");
        Console.WriteLine("  5. Back to main menu");
        Console.WriteLine();
    }

    public void PrintSourceMenu(string currentSource)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  Change Data Source");
        Console.ResetColor();
        Console.WriteLine($"  Current: {GetProviderDisplayName(currentSource)}");
        Console.WriteLine("  " + new string('-', 40));
        Console.WriteLine("  1. Pro Football Reference (HTML scraping)");
        Console.WriteLine("  2. ESPN (JSON API)");
        Console.WriteLine("  3. SportsData.io (requires API key)");
        Console.WriteLine("  4. MySportsFeeds (requires API key)");
        Console.WriteLine("  5. NFL.com (undocumented API)");
        Console.WriteLine("  6. Cancel");
        Console.WriteLine();
    }

    public static bool IsValidProvider(string provider)
    {
        return ValidProviders.Any(p => p.Equals(provider, StringComparison.OrdinalIgnoreCase));
    }

    public static string GetValidProvidersMessage()
    {
        return $"Valid providers: {string.Join(", ", ValidProviders)}";
    }

    public static string GetProviderDisplayName(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "profootballreference" => "Pro Football Reference (HTML)",
            "espn" => "ESPN API",
            "sportsdataio" => "SportsData.io API",
            "mysportsfeeds" => "MySportsFeeds API",
            "nflcom" => "NFL.com API",
            _ => provider
        };
    }

    private static string FormatDbInfo(string dbProvider, string connectionString)
    {
        return dbProvider.ToLowerInvariant() switch
        {
            "sqlite" => ExtractSqlitePath(connectionString),
            "postgresql" => "PostgreSQL",
            "sqlserver" => "SQL Server",
            _ => dbProvider
        };
    }

    private static string ExtractSqlitePath(string connectionString)
    {
        // Extract file path from "Data Source=data/nfl_data.db"
        var parts = connectionString.Split('=', 2);
        var path = parts.Length > 1 ? parts[1].Trim() : connectionString;
        return $"SQLite ({path})";
    }
}
