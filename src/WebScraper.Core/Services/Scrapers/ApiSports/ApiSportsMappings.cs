using System.Globalization;
using System.Text.RegularExpressions;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.ApiSports;

public static partial class ApiSportsMappings
{
    private static readonly Dictionary<int, string> TeamIdToAbbreviationMap = new()
    {
        { 1, "LV" },
        { 2, "JAX" },
        { 3, "NE" },
        { 4, "NYG" },
        { 5, "BAL" },
        { 6, "TEN" },
        { 10, "CIN" },
        { 11, "ARI" },
        { 12, "PHI" },
        { 13, "NYJ" },
        { 14, "SF" },
        { 15, "GB" },
        { 16, "CHI" },
        { 17, "KC" },
        { 18, "WAS" },
        { 20, "BUF" },
        { 21, "IND" },
        { 23, "SEA" },
        { 24, "TB" },
        { 25, "MIA" },
        { 26, "HOU" },
        { 28, "DEN" },
        { 29, "DAL" },
        { 30, "LAC" },
        { 31, "LAR" },
        { 32, "MIN" },
        // TODO(S1b): ids 7, 8, 9, 19, 22, 27 from teams.json
    };

    public static string? TeamIdToAbbreviation(int teamId) =>
        TeamIdToAbbreviationMap.GetValueOrDefault(teamId);

    public static (NflSeasonType SeasonType, int Week)? ParseWeek(string? stage, string? week)
    {
        if (string.IsNullOrWhiteSpace(stage) || string.IsNullOrWhiteSpace(week))
            return null;

        if (stage.Equals("Regular Season", StringComparison.OrdinalIgnoreCase))
        {
            var n = ParseWeekNumber(week);
            return n is null ? null : (NflSeasonType.Regular, n.Value);
        }

        if (stage.Equals("Pre Season", StringComparison.OrdinalIgnoreCase))
        {
            var n = ParseWeekNumber(week);
            return n is null ? null : (NflSeasonType.Preseason, n.Value);
        }

        if (stage.Equals("Post Season", StringComparison.OrdinalIgnoreCase))
        {
            if (week.Equals("Wild Card", StringComparison.OrdinalIgnoreCase))
                return (NflSeasonType.Postseason, 1);
            if (week.Equals("Divisional Round", StringComparison.OrdinalIgnoreCase))
                return (NflSeasonType.Postseason, 2);
            if (week.Equals("Conference Championships", StringComparison.OrdinalIgnoreCase))
                return (NflSeasonType.Postseason, 3);
            if (week.Equals("Super Bowl", StringComparison.OrdinalIgnoreCase))
                return (NflSeasonType.Postseason, 4);
            return null;
        }

        return null;
    }

    public static (string GameStatus, bool IsFinal, bool IsScheduled) ParseStatus(string? shortCode)
    {
        var code = shortCode?.Trim().ToUpperInvariant() ?? string.Empty;

        return code switch
        {
            "FT" or "AOT" => ("Final", true, false),
            "NS" => ("Scheduled", false, true),
            "Q1" or "Q2" or "Q3" or "Q4" or "HT" or "OT" => ("In Progress", false, false),
            "CANC" => ("Cancelled", false, false),
            "PST" => ("Postponed", false, false),
            _ => (code, false, false)
        };
    }

    public static string? ParseHeight(string? height)
    {
        if (string.IsNullOrWhiteSpace(height))
            return null;

        var match = HeightPattern().Match(height.Trim());
        if (!match.Success)
            return null;

        return $"{match.Groups[1].Value}-{match.Groups[2].Value}";
    }

    public static int? ParseWeight(string? weight)
    {
        if (string.IsNullOrWhiteSpace(weight))
            return null;

        var match = WeightPattern().Match(weight.Trim());
        if (!match.Success)
            return null;

        return int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lbs)
            ? lbs
            : null;
    }

    public static bool IsNfl(ApiSportsGameItem item) => item.League.Id == 1;

    private static int? ParseWeekNumber(string week)
    {
        var match = WeekNumberPattern().Match(week.Trim());
        if (!match.Success)
            return null;

        return int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;
    }

    [GeneratedRegex(@"^Week\s+(\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WeekNumberPattern();

    [GeneratedRegex(@"^(\d+)\s*'\s*(\d+)\s*""?$", RegexOptions.CultureInvariant)]
    private static partial Regex HeightPattern();

    [GeneratedRegex(@"^(\d+)\s*lbs\.?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WeightPattern();
}
