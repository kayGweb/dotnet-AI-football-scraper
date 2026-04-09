using WebScraper.Models;

namespace WebScraper.Services.Scrapers.Espn;

/// <summary>
/// Bidirectional mapping between ESPN numeric team IDs and standard NFL abbreviations.
/// ESPN uses its own internal IDs for teams, which differ from standard NFL abbreviations.
/// Conference/division lookups delegate to <see cref="NflTeams"/>.
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
    /// Converts an ESPN numeric team ID to a standard NFL abbreviation,
    /// falling back to the ESPN-provided abbreviation if the ID is unknown.
    /// </summary>
    public static string ToNflAbbreviation(string espnId, string espnAbbreviation)
    {
        if (EspnIdToNflAbbreviation.TryGetValue(espnId, out var mapped))
            return mapped;

        if (!string.IsNullOrEmpty(espnAbbreviation) && NflTeams.IsValid(espnAbbreviation))
            return espnAbbreviation.ToUpperInvariant();

        return espnId;
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
    /// Gets the conference and division for a given NFL abbreviation.
    /// Delegates to <see cref="NflTeams.GetDivision"/>.
    /// </summary>
    public static (string Conference, string Division) GetDivision(string nflAbbreviation)
    {
        return NflTeams.GetDivision(nflAbbreviation);
    }
}
