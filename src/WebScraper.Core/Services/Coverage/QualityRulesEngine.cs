using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Services.Coverage;

/// <summary>
/// Runs cheap post-scrape assertions and persists <see cref="DataQualityFinding"/> rows.
/// </summary>
public class QualityRulesEngine
{
    private readonly AppDbContext _db;

    public QualityRulesEngine(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DataQualityFinding>> RunAsync(
        int? season = null,
        NflSeasonType? seasonType = null,
        int? week = null,
        CancellationToken cancellationToken = default)
    {
        var findings = new List<DataQualityFinding>();

        findings.AddRange(await CheckGamesMissingPlayerStatsAsync(season, seasonType, week, cancellationToken));
        findings.AddRange(await CheckQuarterScoreMismatchAsync(season, seasonType, week, cancellationToken));
        findings.AddRange(await CheckGamesMissingTeamStatsAsync(season, seasonType, week, cancellationToken));
        findings.AddRange(await CheckPlayersMissingEspnIdAsync(cancellationToken));
        findings.AddRange(await CheckImplausiblePassingYardsAsync(season, seasonType, week, cancellationToken));
        findings.AddRange(await CheckVenuesMissingLocationAsync(cancellationToken));
        findings.AddRange(await CheckWeekGameCountMismatchAsync(season, seasonType, week, cancellationToken));

        foreach (var finding in findings)
            await UpsertFindingAsync(finding, cancellationToken);

        return findings;
    }

    public async Task<IReadOnlyList<DataQualityFinding>> GetOpenFindingsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _db.DataQualityFindings
            .Where(f => f.Status == DataQualityStatus.Open || f.Status == DataQualityStatus.RepairQueued)
            .OrderByDescending(f => f.Severity)
            .ThenByDescending(f => f.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private async Task UpsertFindingAsync(DataQualityFinding finding, CancellationToken cancellationToken)
    {
        var existing = await _db.DataQualityFindings
            .FirstOrDefaultAsync(f =>
                f.Status == DataQualityStatus.Open &&
                f.RuleType == finding.RuleType &&
                f.EntityType == finding.EntityType &&
                f.EntityId == finding.EntityId,
                cancellationToken);

        if (existing is null)
        {
            finding.CreatedAt = DateTime.UtcNow;
            _db.DataQualityFindings.Add(finding);
        }
        else
        {
            existing.Message = finding.Message;
            existing.Payload = finding.Payload;
            existing.Severity = finding.Severity;
            existing.Season = finding.Season;
            existing.SeasonType = finding.SeasonType;
            existing.Week = finding.Week;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<DataQualityFinding>> CheckGamesMissingPlayerStatsAsync(
        int? season, NflSeasonType? seasonType, int? week, CancellationToken ct)
    {
        var query = _db.Games
            .Where(g => g.HomeScore != null && g.AwayScore != null);

        if (season.HasValue) query = query.Where(g => g.Season == season.Value);
        if (seasonType.HasValue) query = query.Where(g => g.SeasonType == seasonType.Value);
        if (week.HasValue) query = query.Where(g => g.Week == week.Value);

        var games = await query
            .Select(g => new { g.Id, g.Season, g.SeasonType, g.Week })
            .ToListAsync(ct);

        var gameIdsWithStats = await _db.PlayerGameStats
            .Where(s => games.Select(g => g.Id).Contains(s.GameId))
            .Select(s => s.GameId)
            .Distinct()
            .ToListAsync(ct);

        return games
            .Where(g => !gameIdsWithStats.Contains(g.Id))
            .Select(g => new DataQualityFinding
            {
                RuleType = DataQualityRuleType.GameMissingPlayerStats,
                Severity = DataQualitySeverity.Error,
                EntityType = nameof(Game),
                EntityId = g.Id,
                Season = g.Season,
                SeasonType = g.SeasonType,
                Week = g.Week,
                Message = $"Game {g.Id} is final but has no player stats",
                Payload = JsonSerializer.Serialize(new { gameId = g.Id, repairType = "stats" }),
            })
            .ToList();
    }

    private async Task<List<DataQualityFinding>> CheckQuarterScoreMismatchAsync(
        int? season, NflSeasonType? seasonType, int? week, CancellationToken ct)
    {
        var query = _db.Games.AsQueryable();
        if (season.HasValue) query = query.Where(g => g.Season == season.Value);
        if (seasonType.HasValue) query = query.Where(g => g.SeasonType == seasonType.Value);
        if (week.HasValue) query = query.Where(g => g.Week == week.Value);

        var games = await query.ToListAsync(ct);
        var findings = new List<DataQualityFinding>();

        foreach (var g in games)
        {
            if (g.HomeScore is null || g.AwayScore is null)
                continue;

            var homeQuarters = new[] { g.HomeQ1, g.HomeQ2, g.HomeQ3, g.HomeQ4, g.HomeOT };
            var awayQuarters = new[] { g.AwayQ1, g.AwayQ2, g.AwayQ3, g.AwayQ4, g.AwayOT };

            if (homeQuarters.All(q => q is null) && awayQuarters.All(q => q is null))
                continue;

            var homeSum = homeQuarters.Where(q => q.HasValue).Sum(q => q!.Value);
            var awaySum = awayQuarters.Where(q => q.HasValue).Sum(q => q!.Value);

            if (homeSum != g.HomeScore || awaySum != g.AwayScore)
            {
                findings.Add(new DataQualityFinding
                {
                    RuleType = DataQualityRuleType.QuarterScoresMismatch,
                    Severity = DataQualitySeverity.Warning,
                    EntityType = nameof(Game),
                    EntityId = g.Id,
                    Season = g.Season,
                    SeasonType = g.SeasonType,
                    Week = g.Week,
                    Message = $"Game {g.Id}: quarter sums ({homeSum}-{awaySum}) != final ({g.HomeScore}-{g.AwayScore})",
                    Payload = JsonSerializer.Serialize(new { gameId = g.Id, repairType = "games" }),
                });
            }
        }

        return findings;
    }

    private async Task<List<DataQualityFinding>> CheckGamesMissingTeamStatsAsync(
        int? season, NflSeasonType? seasonType, int? week, CancellationToken ct)
    {
        var query = _db.Games.AsQueryable();
        if (season.HasValue) query = query.Where(g => g.Season == season.Value);
        if (seasonType.HasValue) query = query.Where(g => g.SeasonType == seasonType.Value);
        if (week.HasValue) query = query.Where(g => g.Week == week.Value);

        var games = await query.Select(g => new { g.Id, g.Season, g.SeasonType, g.Week }).ToListAsync(ct);

        var statsByGame = await _db.TeamGameStats
            .Where(s => games.Select(g => g.Id).Contains(s.GameId))
            .GroupBy(s => s.GameId)
            .Select(g => new { GameId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return games
            .Where(g =>
            {
                var entry = statsByGame.FirstOrDefault(s => s.GameId == g.Id);
                return entry is null || entry.Count < 2;
            })
            .Select(g => new DataQualityFinding
            {
                RuleType = DataQualityRuleType.GameMissingTeamStats,
                Severity = DataQualitySeverity.Warning,
                EntityType = nameof(Game),
                EntityId = g.Id,
                Season = g.Season,
                SeasonType = g.SeasonType,
                Week = g.Week,
                Message = $"Game {g.Id} is missing team stats for one or both sides",
                Payload = JsonSerializer.Serialize(new { gameId = g.Id, repairType = "stats" }),
            })
            .ToList();
    }

    private async Task<List<DataQualityFinding>> CheckPlayersMissingEspnIdAsync(CancellationToken ct)
    {
        var players = await _db.Players
            .Where(p => p.EspnId == null || p.EspnId == "")
            .Take(500)
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(ct);

        return players.Select(p => new DataQualityFinding
        {
            RuleType = DataQualityRuleType.PlayerMissingEspnId,
            Severity = DataQualitySeverity.Info,
            EntityType = nameof(Player),
            EntityId = p.Id,
            Message = $"Player '{p.Name}' (id={p.Id}) has no EspnId",
        }).ToList();
    }

    private async Task<List<DataQualityFinding>> CheckImplausiblePassingYardsAsync(
        int? season, NflSeasonType? seasonType, int? week, CancellationToken ct)
    {
        var query = _db.PlayerGameStats.AsQueryable();

        if (season.HasValue || seasonType.HasValue || week.HasValue)
        {
            query = query.Where(s => _db.Games.Any(g =>
                g.Id == s.GameId &&
                (!season.HasValue || g.Season == season.Value) &&
                (!seasonType.HasValue || g.SeasonType == seasonType.Value) &&
                (!week.HasValue || g.Week == week.Value)));
        }

        var stats = await query
            .Where(s => s.PassYards > 600)
            .Select(s => new { s.Id, s.GameId, s.PlayerId, s.PassYards, s.Game.Season, s.Game.SeasonType, s.Game.Week })
            .Take(100)
            .ToListAsync(ct);

        return stats.Select(s => new DataQualityFinding
        {
            RuleType = DataQualityRuleType.ImplausiblePassingYards,
            Severity = DataQualitySeverity.Warning,
            EntityType = nameof(PlayerGameStats),
            EntityId = s.Id,
            Season = s.Season,
            SeasonType = s.SeasonType,
            Week = s.Week,
            Message = $"Stat line {s.Id}: {s.PassYards} passing yards (likely parse error)",
            Payload = JsonSerializer.Serialize(new { gameId = s.GameId, repairType = "stats" }),
        }).ToList();
    }

    private async Task<List<DataQualityFinding>> CheckVenuesMissingLocationAsync(CancellationToken ct)
    {
        var venues = await _db.Venues
            .Where(v => v.City == "" || v.State == "")
            .Take(200)
            .Select(v => new { v.Id, v.Name })
            .ToListAsync(ct);

        return venues.Select(v => new DataQualityFinding
        {
            RuleType = DataQualityRuleType.VenueMissingLocation,
            Severity = DataQualitySeverity.Info,
            EntityType = nameof(Venue),
            EntityId = v.Id,
            Message = $"Venue '{v.Name}' (id={v.Id}) is missing city or state",
        }).ToList();
    }

    private async Task<List<DataQualityFinding>> CheckWeekGameCountMismatchAsync(
        int? season, NflSeasonType? seasonType, int? week, CancellationToken ct)
    {
        var findings = new List<DataQualityFinding>();

        List<int> seasons;
        if (season.HasValue)
            seasons = new List<int> { season.Value };
        else
            seasons = await _db.Games.Select(g => g.Season).Distinct().ToListAsync(ct);

        foreach (var s in seasons)
        {
            var types = seasonType.HasValue
                ? new[] { seasonType.Value }
                : Enum.GetValues<NflSeasonType>();

            foreach (var st in types)
            {
                var weeks = week.HasValue
                    ? new[] { week.Value }
                    : Enumerable.Range(1, NflSeasonSchedule.GetScoreboardWeeks(st, s));

                foreach (var w in weeks)
                {
                    var expected = NflSeasonSchedule.GetExpectedGamesForWeek(st, s, w);
                    if (expected is null)
                        continue;

                    var actual = await _db.Games
                        .CountAsync(g => g.Season == s && g.SeasonType == st && g.Week == w, ct);

                    if (actual != expected.Value)
                    {
                        findings.Add(new DataQualityFinding
                        {
                            RuleType = DataQualityRuleType.WeekGameCountMismatch,
                            Severity = actual < expected ? DataQualitySeverity.Error : DataQualitySeverity.Warning,
                            EntityType = "Week",
                            Season = s,
                            SeasonType = st,
                            Week = w,
                            Message = $"{s} {st} week {w}: expected {expected} games, found {actual}",
                            Payload = JsonSerializer.Serialize(new { season = s, seasonType = st, week = w, repairType = "games" }),
                        });
                    }
                }
            }
        }

        return findings;
    }
}
