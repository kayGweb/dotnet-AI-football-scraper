using Microsoft.EntityFrameworkCore;
using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Services.Agent;

/// <summary>
/// Parameterized aggregation over a fixed whitelist — no raw SQL (AGENT_PLATFORM_PLAN §2).
/// </summary>
public class QueryStatsService
{
    private const int MaxLimit = 500;
    private const int MaxGroupBy = 3;

    private static readonly HashSet<string> PlayerMeasures = new(StringComparer.OrdinalIgnoreCase)
    {
        "passYards", "passTouchdowns", "rushYards", "rushTouchdowns", "receivingYards",
        "receivingTouchdowns", "receptions", "totalTackles", "defensiveSacks",
        "fieldGoalsMade", "totalKickingPoints",
    };

    private static readonly HashSet<string> PlayerDimensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "season", "seasonType", "week", "player", "position", "team",
    };

    private readonly AppDbContext _db;

    public QueryStatsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<object> ExecuteAsync(QueryStatsRequest request, CancellationToken ct = default)
    {
        if (!string.Equals(request.Dataset, "player_game_stats", StringComparison.OrdinalIgnoreCase))
            return new { error = true, message = "Only dataset 'player_game_stats' is supported initially." };

        if (!PlayerMeasures.Contains(request.Measure))
            return new { error = true, message = $"Unknown measure '{request.Measure}'.", allowed = PlayerMeasures };

        if (request.GroupBy.Count > MaxGroupBy)
            return new { error = true, message = $"groupBy supports at most {MaxGroupBy} dimensions." };

        foreach (var dim in request.GroupBy)
        {
            if (!PlayerDimensions.Contains(dim))
                return new { error = true, message = $"Unknown groupBy dimension '{dim}'.", allowed = PlayerDimensions };
        }

        var limit = Math.Clamp(request.Limit, 1, MaxLimit);
        var agg = request.Aggregation.ToLowerInvariant();

        if (agg is not ("sum" or "avg" or "min" or "max" or "count"))
            return new { error = true, message = "aggregation must be sum, avg, min, max, or count." };

        var query = _db.PlayerGameStats
            .AsNoTracking()
            .Include(s => s.Player).ThenInclude(p => p.Team)
            .Include(s => s.Game)
            .AsQueryable();

        query = ApplyFilters(query, request.Filters);

        var projected = query.Select(s => new StatsRow
        {
            Season = s.Game.Season,
            SeasonType = s.Game.SeasonType,
            Week = s.Game.Week,
            Player = s.Player.Name,
            Position = s.Player.Position,
            Team = s.Player.Team != null ? s.Player.Team.Abbreviation : "",
            PassYards = s.PassYards,
            PassTouchdowns = s.PassTouchdowns,
            RushYards = s.RushYards,
            RushTouchdowns = s.RushTouchdowns,
            ReceivingYards = s.ReceivingYards,
            ReceivingTouchdowns = s.ReceivingTouchdowns,
            Receptions = s.Receptions,
            TotalTackles = s.TotalTackles,
            DefensiveSacks = s.DefensiveSacks,
            FieldGoalsMade = s.FieldGoalsMade,
            TotalKickingPoints = s.TotalKickingPoints,
        });

        var rows = await projected.ToListAsync(ct);
        var grouped = rows
            .GroupBy(r => BuildGroupKey(r, request.GroupBy))
            .Select(g => new
            {
                keys = g.Key,
                value = Aggregate(g, request.Measure, agg),
            });

        if (!string.IsNullOrEmpty(request.OrderBy) &&
            string.Equals(request.OrderBy, request.Measure, StringComparison.OrdinalIgnoreCase))
        {
            grouped = request.OrderDescending
                ? grouped.OrderByDescending(x => x.value)
                : grouped.OrderBy(x => x.value);
        }
        else
        {
            grouped = grouped.OrderByDescending(x => x.value);
        }

        var results = grouped.Take(limit).ToList();

        return new
        {
            dataset = request.Dataset,
            measure = request.Measure,
            aggregation = agg,
            groupBy = request.GroupBy,
            rowCount = results.Count,
            results,
        };
    }

    private static IQueryable<PlayerGameStats> ApplyFilters(
        IQueryable<PlayerGameStats> query, List<QueryStatsFilter> filters)
    {
        foreach (var f in filters)
        {
            if (!int.TryParse(f.Value, out var intVal) &&
                f.Field is "season" or "week")
                continue;

            query = f.Field.ToLowerInvariant() switch
            {
                "season" when f.Op == "=" => query.Where(s => s.Game.Season == intVal),
                "week" when f.Op == "=" => query.Where(s => s.Game.Week == intVal),
                "seasonType" when f.Op == "=" && Enum.TryParse<NflSeasonType>(f.Value, true, out var st)
                    => query.Where(s => s.Game.SeasonType == st),
                "position" when f.Op == "=" => query.Where(s => s.Player.Position == f.Value),
                "team" when f.Op == "=" => query.Where(s => s.Player.Team != null && s.Player.Team.Abbreviation == f.Value),
                "player" when f.Op == "=" => query.Where(s => s.Player.Name == f.Value),
                _ => query,
            };
        }

        return query;
    }

    private static Dictionary<string, object?> BuildGroupKey(StatsRow row, List<string> dimensions)
    {
        var key = new Dictionary<string, object?>();
        foreach (var dim in dimensions)
        {
            key[dim] = dim.ToLowerInvariant() switch
            {
                "season" => row.Season,
                "seasonType" => row.SeasonType.ToString(),
                "week" => row.Week,
                "player" => row.Player,
                "position" => row.Position,
                "team" => row.Team,
                _ => null,
            };
        }
        return key;
    }

    private static double Aggregate(IEnumerable<StatsRow> rows, string measure, string agg)
    {
        var values = measure.ToLowerInvariant() switch
        {
            "passyards" => rows.Select(r => (double)r.PassYards),
            "passtouchdowns" => rows.Select(r => (double)r.PassTouchdowns),
            "rushyards" => rows.Select(r => (double)r.RushYards),
            "rushtouchdowns" => rows.Select(r => (double)r.RushTouchdowns),
            "receivingyards" => rows.Select(r => (double)r.ReceivingYards),
            "receivingtouchdowns" => rows.Select(r => (double)r.ReceivingTouchdowns),
            "receptions" => rows.Select(r => (double)r.Receptions),
            "totaltackles" => rows.Select(r => (double)r.TotalTackles),
            "defensivesacks" => rows.Select(r => r.DefensiveSacks),
            "fieldgoalsmade" => rows.Select(r => (double)r.FieldGoalsMade),
            "totalkickingpoints" => rows.Select(r => (double)r.TotalKickingPoints),
            _ => rows.Select(_ => 0.0),
        };

        return agg switch
        {
            "sum" => values.Sum(),
            "avg" => values.Any() ? values.Average() : 0,
            "min" => values.Any() ? values.Min() : 0,
            "max" => values.Any() ? values.Max() : 0,
            "count" => values.Count(),
            _ => 0,
        };
    }

    private sealed class StatsRow
    {
        public int Season { get; init; }
        public NflSeasonType SeasonType { get; init; }
        public int Week { get; init; }
        public string Player { get; init; } = "";
        public string Position { get; init; } = "";
        public string Team { get; init; } = "";
        public int PassYards { get; init; }
        public int PassTouchdowns { get; init; }
        public int RushYards { get; init; }
        public int RushTouchdowns { get; init; }
        public int ReceivingYards { get; init; }
        public int ReceivingTouchdowns { get; init; }
        public int Receptions { get; init; }
        public int TotalTackles { get; init; }
        public double DefensiveSacks { get; init; }
        public int FieldGoalsMade { get; init; }
        public int TotalKickingPoints { get; init; }
    }
}
