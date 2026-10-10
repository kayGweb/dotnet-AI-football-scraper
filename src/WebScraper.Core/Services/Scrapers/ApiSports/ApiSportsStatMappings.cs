using System.Globalization;
using System.Text.Json;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.ApiSports;

/// <summary>
/// Pure mapping from api-sports box-score payloads onto <see cref="PlayerGameStats"/> and
/// <see cref="TeamGameStats"/>. No HTTP, no DI — unit-tested against the
/// <c>game-player-stats.json</c> / <c>game-team-stats.json</c> fixtures.
///
/// Stat names follow the vendor documentation for <c>/games/statistics/players</c> and
/// <c>/games/statistics/teams</c>. Matching is case-insensitive and tolerant: unknown names are
/// ignored, missing names leave the column at its default, and a handful of aliases are accepted
/// so a renamed field degrades to a zero rather than an exception.
/// </summary>
public static class ApiSportsStatMappings
{
    public const string GroupPassing = "passing";
    public const string GroupRushing = "rushing";
    public const string GroupReceiving = "receiving";
    public const string GroupFumbles = "fumbles";
    public const string GroupDefensive = "defensive";
    public const string GroupInterceptions = "interceptions";
    public const string GroupKickReturns = "kick returns";
    public const string GroupPuntReturns = "punt returns";
    public const string GroupKicking = "kicking";
    public const string GroupPunting = "punting";

    /// <summary>Flattens a stat line's <c>statistics</c> array into a case-insensitive name → text map.</summary>
    public static Dictionary<string, string?> ToValueMap(IEnumerable<ApiSportsStatValue> statistics)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var stat in statistics)
        {
            if (string.IsNullOrWhiteSpace(stat.Name))
                continue;
            map[stat.Name.Trim()] = stat.Text;
        }
        return map;
    }

    public static string NormalizeGroupName(string? groupName) =>
        (groupName ?? string.Empty).Trim().ToLowerInvariant().Replace('_', ' ');

    /// <summary>Applies one group's values onto the accumulating per-player stat line.</summary>
    public static void ApplyPlayerGroup(PlayerGameStats stats, string? groupName, IReadOnlyDictionary<string, string?> values)
    {
        switch (NormalizeGroupName(groupName))
        {
            case GroupPassing:
                Ratio(values, out var comp, out var att, "comp_att", "completions_attempts", "c/att");
                stats.PassCompletions = comp;
                stats.PassAttempts = att;
                stats.PassYards = Int(values, "yards", "passing_yards");
                stats.PassTouchdowns = Int(values, "passing_touch_downs", "touchdowns", "td");
                stats.Interceptions = Int(values, "interceptions", "int");
                Ratio(values, out var sacks, out var sackYards, "sacks", "sacks_yards_lost");
                stats.Sacks = sacks;
                stats.SackYardsLost = sackYards;
                stats.QBRating = NullableDouble(values, "rating", "qb_rating", "rtg");
                break;

            case GroupRushing:
                stats.RushAttempts = Int(values, "total_rushes", "rushes", "attempts", "car");
                stats.RushYards = Int(values, "yards", "rushing_yards");
                stats.RushTouchdowns = Int(values, "rushing_touch_downs", "touchdowns", "td");
                stats.LongRushing = Int(values, "longest_rush", "long", "lg");
                break;

            case GroupReceiving:
                stats.Receptions = Int(values, "total_receptions", "receptions", "rec");
                stats.ReceivingYards = Int(values, "yards", "receiving_yards");
                stats.ReceivingTouchdowns = Int(values, "receiving_touch_downs", "touchdowns", "td");
                stats.LongReception = Int(values, "longest_reception", "long", "lg");
                stats.ReceivingTargets = Int(values, "targets", "tgts");
                stats.YardsPerReception = Double(values, "average", "avg", "yards_per_reception");
                break;

            case GroupFumbles:
                stats.Fumbles = Int(values, "fumbles", "fum", "total");
                stats.FumblesLost = Int(values, "lost", "fumbles_lost");
                stats.FumblesRecovered = Int(values, "recovered", "fumbles_recovered", "rec");
                break;

            case GroupDefensive:
                stats.TotalTackles = Int(values, "tackles", "total_tackles", "tot");
                stats.SoloTackles = Int(values, "unassisted_tackles", "solo_tackles", "solo");
                stats.DefensiveSacks = Double(values, "sacks");
                stats.TacklesForLoss = Int(values, "tfl", "tackles_for_loss");
                stats.PassesDefended = Int(values, "passes_defended", "pd");
                stats.QBHits = Int(values, "qb_hts", "qb_hits", "qb_hit");
                stats.DefensiveTouchdowns = Int(values, "td", "touchdowns", "defensive_touch_downs");
                break;

            case GroupInterceptions:
                stats.InterceptionsCaught = Int(values, "total_interceptions", "interceptions", "int");
                stats.InterceptionYards = Int(values, "yards", "interception_yards");
                stats.InterceptionTouchdowns = Int(values, "intercepted_touch_downs", "touchdowns", "td");
                break;

            case GroupKickReturns:
                stats.KickReturns = Int(values, "total", "returns", "no");
                stats.KickReturnYards = Int(values, "yards");
                stats.LongKickReturn = Int(values, "lg", "long", "longest");
                stats.KickReturnTouchdowns = Int(values, "td", "touchdowns");
                break;

            case GroupPuntReturns:
                stats.PuntReturns = Int(values, "total", "returns", "no");
                stats.PuntReturnYards = Int(values, "yards");
                stats.LongPuntReturn = Int(values, "lg", "long", "longest");
                stats.PuntReturnTouchdowns = Int(values, "td", "touchdowns");
                break;

            case GroupKicking:
                Ratio(values, out var fgm, out var fga, "field_goals", "fg");
                stats.FieldGoalsMade = fgm;
                stats.FieldGoalAttempts = fga;
                stats.LongFieldGoal = Int(values, "long", "lg", "longest");
                Ratio(values, out var xpm, out var xpa, "extra_points", "xp");
                stats.ExtraPointsMade = xpm;
                stats.ExtraPointAttempts = xpa;
                stats.TotalKickingPoints = Int(values, "points", "pts");
                break;

            case GroupPunting:
                stats.Punts = Int(values, "total", "punts", "no");
                stats.PuntYards = Int(values, "yards");
                stats.GrossAvgPuntYards = Double(values, "average", "avg");
                stats.PuntTouchbacks = Int(values, "touchbacks", "tb");
                stats.PuntsInside20 = Int(values, "inside_20", "in20", "inside20");
                stats.LongPunt = Int(values, "long", "lg", "longest");
                break;
        }
    }

    /// <summary>Maps the <c>statistics</c> object of <c>/games/statistics/teams</c> onto <see cref="TeamGameStats"/>.</summary>
    public static void MapTeamStatistics(TeamGameStats tgs, IReadOnlyDictionary<string, JsonElement> statistics)
    {
        var firstDowns = Section(statistics, "first_downs");
        tgs.FirstDowns = Int(firstDowns, "total");
        tgs.FirstDownsPassing = Int(firstDowns, "passing");
        tgs.FirstDownsRushing = Int(firstDowns, "rushing");
        tgs.FirstDownsPenalty = Int(firstDowns, "from_penalties", "penalty");
        Ratio(firstDowns, out var tdm, out var tda, "third_down_efficiency");
        tgs.ThirdDownMade = tdm;
        tgs.ThirdDownAttempts = tda;
        Ratio(firstDowns, out var fdm, out var fda, "fourth_down_efficiency");
        tgs.FourthDownMade = fdm;
        tgs.FourthDownAttempts = fda;

        tgs.TotalPlays = Int(Section(statistics, "plays"), "total");
        tgs.TotalYards = Int(Section(statistics, "yards"), "total");

        var passing = Section(statistics, "passing");
        tgs.NetPassingYards = Int(passing, "total");
        Ratio(passing, out var pc, out var pa, "comp_att");
        tgs.PassCompletions = pc;
        tgs.PassAttempts = pa;
        tgs.YardsPerPass = Double(passing, "yards_per_pass");
        tgs.InterceptionsThrown = Int(passing, "interceptions_thrown", "interceptions");
        Ratio(passing, out var sacks, out var sackYards, "sacks_yards_lost");
        tgs.SacksAgainst = sacks;
        tgs.SackYardsLost = sackYards;

        var rushing = Section(statistics, "rushings", "rushing");
        tgs.RushingYards = Int(rushing, "total");
        tgs.RushingAttempts = Int(rushing, "attempts");
        tgs.YardsPerRush = Double(rushing, "yards_per_rush");

        Ratio(Section(statistics, "red_zone"), out var rzm, out var rza, "made_att");
        tgs.RedZoneMade = rzm;
        tgs.RedZoneAttempts = rza;

        Ratio(Section(statistics, "penalties"), out var pen, out var penYards, "total");
        tgs.Penalties = pen;
        tgs.PenaltyYards = penYards;

        var turnovers = Section(statistics, "turnovers");
        tgs.Turnovers = Int(turnovers, "total");
        tgs.FumblesLost = Int(turnovers, "lost_fumbles", "fumbles");

        tgs.DefensiveTouchdowns = Int(Section(statistics, "int_touchdowns"), "total");

        // The vendor spells it "posession"; accept the correct spelling too in case they fix it.
        var possession = Text(Section(statistics, "posession", "possession"), "total");
        tgs.PossessionTime = possession ?? string.Empty;
    }

    // ----- helpers -------------------------------------------------------------------------

    private static IReadOnlyDictionary<string, string?> Section(
        IReadOnlyDictionary<string, JsonElement> statistics,
        params string[] names)
    {
        foreach (var name in names)
        {
            var match = statistics.FirstOrDefault(kv => kv.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match.Key == null || match.Value.ValueKind != JsonValueKind.Object)
                continue;

            var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in match.Value.EnumerateObject())
                map[property.Name] = ApiSportsJson.ElementToText(property.Value);
            return map;
        }

        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }

    private static string? Text(IReadOnlyDictionary<string, string?> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text))
                return text.Trim();
        }
        return null;
    }

    private static int Int(IReadOnlyDictionary<string, string?> values, params string[] keys)
    {
        var text = Text(values, keys);
        if (text == null)
            return 0;

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            return i;

        // "1.0" style numbers, or a ratio where only the first number matters ("1-5" → 1)
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return (int)Math.Round(d);

        var head = text.Split('/', '-')[0];
        return int.TryParse(head, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) ? h : 0;
    }

    private static double Double(IReadOnlyDictionary<string, string?> values, params string[] keys)
    {
        var text = Text(values, keys);
        return text != null && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0d;
    }

    private static double? NullableDouble(IReadOnlyDictionary<string, string?> values, params string[] keys)
    {
        var text = Text(values, keys);
        return text != null && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    /// <summary>Parses "24/36", "3-19" or "2/3" into two ints; anything else yields zeros.</summary>
    private static void Ratio(IReadOnlyDictionary<string, string?> values, out int first, out int second, params string[] keys)
    {
        first = 0;
        second = 0;
        var text = Text(values, keys);
        if (text == null)
            return;

        var parts = text.Split('/', '-');
        if (parts.Length != 2)
            return;

        int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out first);
        int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out second);
    }
}
