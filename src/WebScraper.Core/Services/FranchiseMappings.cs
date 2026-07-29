namespace WebScraper.Services;

/// <summary>
/// Maps season-specific team abbreviations to canonical franchise abbreviations.
/// Covers post-1970 relocations and rebrandings per AGENT_PLATFORM_PLAN §1.4.
/// </summary>
public static class FranchiseMappings
{
    private static readonly Dictionary<string, string> CanonicalByAbbreviation = new(StringComparer.OrdinalIgnoreCase)
    {
        // Rams: STL → LA
        ["STL"] = "LAR",
        // Chargers: SD → LAC
        ["SD"] = "LAC",
        // Raiders: OAK → LV
        ["OAK"] = "LV",
        // Washington rebrandings
        ["WSH"] = "WAS",
        ["WFT"] = "WAS",
        // Cardinals historical
        ["PHX"] = "ARI",
        ["STL_CARD"] = "ARI",
        // Colts
        ["BAL"] = "IND",
        // Oilers → Titans
        ["HOU_OIL"] = "TEN",
        ["OIL"] = "TEN",
    };

    public static string ToCanonicalAbbreviation(string abbreviation)
    {
        if (string.IsNullOrWhiteSpace(abbreviation))
            return abbreviation;

        var upper = abbreviation.ToUpperInvariant();
        return CanonicalByAbbreviation.GetValueOrDefault(upper, upper);
    }
}
