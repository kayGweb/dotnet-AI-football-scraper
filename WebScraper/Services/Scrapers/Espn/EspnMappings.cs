namespace WebScraper.Services.Scrapers.Espn;

/// <summary>
/// Bidirectional mapping between ESPN numeric team IDs and standard NFL abbreviations.
/// ESPN uses its own internal IDs for teams, which differ from standard NFL abbreviations.
/// </summary>
public static class EspnMappings
{
    private static readonly Dictionary<string, string> EspnIdToNflAbbreviation = new()
    {
        { "1", "ATL" },
        { "2", "BUF" },
        { "3", "CHI" },
        { "4", "CIN" },
        { "5", "CLE" },
        { "6", "DAL" },
        { "7", "DEN" },
        { "8", "DET" },
        { "9", "GB" },
        { "10", "TEN" },
        { "11", "IND" },
        { "12", "KC" },
        { "13", "LV" },
        { "14", "LAR" },
        { "15", "MIA" },
        { "16", "MIN" },
        { "17", "NE" },
        { "18", "NO" },
        { "19", "NYG" },
        { "20", "NYJ" },
        { "21", "PHI" },
        { "22", "ARI" },
        { "23", "PIT" },
        { "24", "LAC" },
        { "25", "SF" },
        { "26", "SEA" },
        { "27", "TB" },
        { "28", "WAS" },
        { "29", "CAR" },
        { "30", "JAX" },
        { "33", "BAL" },
        { "34", "HOU" },
    };

    private static readonly Dictionary<string, string> NflAbbreviationToEspnId;

    static EspnMappings()
    {
        NflAbbreviationToEspnId = new Dictionary<string, string>();
        foreach (var kvp in EspnIdToNflAbbreviation)
        {
            NflAbbreviationToEspnId[kvp.Value] = kvp.Key;
        }
    }

    /// <summary>
    /// Converts an ESPN numeric team ID to a standard NFL abbreviation.
    /// Returns the ID unchanged if no mapping exists.
    /// </summary>
    public static string ToNflAbbreviation(string espnId)
    {
        return EspnIdToNflAbbreviation.GetValueOrDefault(espnId, espnId);
    }

    /// <summary>
    /// Converts a standard NFL abbreviation to an ESPN numeric team ID.
    /// Returns null if no mapping exists.
    /// </summary>
    public static string? ToEspnId(string nflAbbreviation)
    {
        return NflAbbreviationToEspnId.GetValueOrDefault(nflAbbreviation.ToUpperInvariant());
    }

    /// <summary>
    /// Conference and division lookup by NFL abbreviation.
    /// </summary>
    private static readonly Dictionary<string, (string Conference, string Division)> DivisionLookup = new()
    {
        // AFC East
        { "BUF", ("AFC", "East") }, { "MIA", ("AFC", "East") }, { "NE", ("AFC", "East") }, { "NYJ", ("AFC", "East") },
        // AFC North
        { "BAL", ("AFC", "North") }, { "CIN", ("AFC", "North") }, { "CLE", ("AFC", "North") }, { "PIT", ("AFC", "North") },
        // AFC South
        { "HOU", ("AFC", "South") }, { "IND", ("AFC", "South") }, { "JAX", ("AFC", "South") }, { "TEN", ("AFC", "South") },
        // AFC West
        { "DEN", ("AFC", "West") }, { "KC", ("AFC", "West") }, { "LV", ("AFC", "West") }, { "LAC", ("AFC", "West") },
        // NFC East
        { "DAL", ("NFC", "East") }, { "NYG", ("NFC", "East") }, { "PHI", ("NFC", "East") }, { "WAS", ("NFC", "East") },
        // NFC North
        { "CHI", ("NFC", "North") }, { "DET", ("NFC", "North") }, { "GB", ("NFC", "North") }, { "MIN", ("NFC", "North") },
        // NFC South
        { "ATL", ("NFC", "South") }, { "CAR", ("NFC", "South") }, { "NO", ("NFC", "South") }, { "TB", ("NFC", "South") },
        // NFC West
        { "ARI", ("NFC", "West") }, { "LAR", ("NFC", "West") }, { "SF", ("NFC", "West") }, { "SEA", ("NFC", "West") },
    };

    /// <summary>
    /// Gets the conference and division for a given NFL abbreviation.
    /// </summary>
    public static (string Conference, string Division) GetDivision(string nflAbbreviation)
    {
        return DivisionLookup.GetValueOrDefault(nflAbbreviation.ToUpperInvariant(), ("", ""));
    }
}
