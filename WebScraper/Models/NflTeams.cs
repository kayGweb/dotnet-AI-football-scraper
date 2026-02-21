namespace WebScraper.Models;

/// <summary>
/// Canonical source of truth for all 32 NFL team abbreviations, conferences, and divisions.
/// Provider-specific mappings (e.g., ESPN IDs) should reference this rather than
/// maintaining their own copy of the division/conference data.
/// </summary>
public static class NflTeams
{
    public record TeamInfo(string Abbreviation, string Conference, string Division);

    private static readonly TeamInfo[] AllTeams =
    [
        // AFC East
        new("BUF", "AFC", "East"),
        new("MIA", "AFC", "East"),
        new("NE",  "AFC", "East"),
        new("NYJ", "AFC", "East"),
        // AFC North
        new("BAL", "AFC", "North"),
        new("CIN", "AFC", "North"),
        new("CLE", "AFC", "North"),
        new("PIT", "AFC", "North"),
        // AFC South
        new("HOU", "AFC", "South"),
        new("IND", "AFC", "South"),
        new("JAX", "AFC", "South"),
        new("TEN", "AFC", "South"),
        // AFC West
        new("DEN", "AFC", "West"),
        new("KC",  "AFC", "West"),
        new("LV",  "AFC", "West"),
        new("LAC", "AFC", "West"),
        // NFC East
        new("DAL", "NFC", "East"),
        new("NYG", "NFC", "East"),
        new("PHI", "NFC", "East"),
        new("WAS", "NFC", "East"),
        // NFC North
        new("CHI", "NFC", "North"),
        new("DET", "NFC", "North"),
        new("GB",  "NFC", "North"),
        new("MIN", "NFC", "North"),
        // NFC South
        new("ATL", "NFC", "South"),
        new("CAR", "NFC", "South"),
        new("NO",  "NFC", "South"),
        new("TB",  "NFC", "South"),
        // NFC West
        new("ARI", "NFC", "West"),
        new("LAR", "NFC", "West"),
        new("SF",  "NFC", "West"),
        new("SEA", "NFC", "West"),
    ];

    private static readonly Dictionary<string, TeamInfo> ByAbbreviation;

    static NflTeams()
    {
        ByAbbreviation = new Dictionary<string, TeamInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var team in AllTeams)
        {
            ByAbbreviation[team.Abbreviation] = team;
        }
    }

    /// <summary>All 32 NFL teams in division order.</summary>
    public static IReadOnlyList<TeamInfo> All => AllTeams;

    /// <summary>All valid NFL abbreviations (uppercase).</summary>
    public static IReadOnlyCollection<string> Abbreviations => ByAbbreviation.Keys;

    /// <summary>Returns true if the abbreviation matches a known NFL team (case-insensitive).</summary>
    public static bool IsValid(string abbreviation)
        => ByAbbreviation.ContainsKey(abbreviation);

    /// <summary>
    /// Gets the conference and division for an NFL abbreviation.
    /// Returns empty strings if the abbreviation is unknown.
    /// </summary>
    public static (string Conference, string Division) GetDivision(string abbreviation)
    {
        if (ByAbbreviation.TryGetValue(abbreviation, out var info))
            return (info.Conference, info.Division);
        return ("", "");
    }

    /// <summary>
    /// Teams grouped by "Conference Division" key (e.g., "AFC East"),
    /// with divisions in standard display order.
    /// </summary>
    public static IEnumerable<IGrouping<string, TeamInfo>> ByDivision()
        => AllTeams.GroupBy(t => $"{t.Conference} {t.Division}");
}
