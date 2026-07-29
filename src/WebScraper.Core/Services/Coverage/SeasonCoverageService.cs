using Microsoft.EntityFrameworkCore;
using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Services.Coverage;

/// <summary>
/// Computes expected-vs-actual coverage per (season, seasonType, week) and persists snapshots.
/// </summary>
public class SeasonCoverageService
{
    private readonly AppDbContext _db;

    public SeasonCoverageService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SeasonCoverage>> RefreshAsync(
        int? season = null,
        NflSeasonType? seasonType = null,
        CancellationToken cancellationToken = default)
    {
        List<int> seasons;
        if (season.HasValue)
            seasons = new List<int> { season.Value };
        else
            seasons = await _db.Games.Select(g => g.Season).Distinct().OrderBy(s => s).ToListAsync(cancellationToken);

        if (seasons.Count == 0)
            seasons = Enumerable.Range(
                NflSeasonSchedule.TwentyYearBackfillStartSeason,
                NflSeasonSchedule.TwentyYearBackfillEndSeason - NflSeasonSchedule.TwentyYearBackfillStartSeason + 1).ToList();

        var results = new List<SeasonCoverage>();

        foreach (var s in seasons)
        {
            var types = seasonType.HasValue
                ? new[] { seasonType.Value }
                : Enum.GetValues<NflSeasonType>();

            foreach (var st in types)
            {
                var weeks = NflSeasonSchedule.GetScoreboardWeeks(st, s);
                for (var week = 1; week <= weeks; week++)
                {
                    var snapshot = await ComputeWeekAsync(s, st, week, cancellationToken);
                    await UpsertAsync(snapshot, cancellationToken);
                    results.Add(snapshot);
                }
            }
        }

        return results;
    }

    public async Task<SeasonCoverage> ComputeWeekAsync(
        int season,
        NflSeasonType seasonType,
        int week,
        CancellationToken cancellationToken = default)
    {
        var games = await _db.Games
            .Where(g => g.Season == season && g.SeasonType == seasonType && g.Week == week)
            .Select(g => new { g.Id, g.GameStatus })
            .ToListAsync(cancellationToken);

        var gameIds = games.Select(g => g.Id).ToList();

        var gamesWithPlayerStats = gameIds.Count == 0
            ? 0
            : await _db.PlayerGameStats
                .Where(s => gameIds.Contains(s.GameId))
                .Select(s => s.GameId)
                .Distinct()
                .CountAsync(cancellationToken);

        var gamesWithTeamStats = gameIds.Count == 0
            ? 0
            : await _db.TeamGameStats
                .Where(s => gameIds.Contains(s.GameId))
                .Select(s => s.GameId)
                .Distinct()
                .CountAsync(cancellationToken);

        var gamesWithInjuries = gameIds.Count == 0
            ? 0
            : await _db.Injuries
                .Where(i => gameIds.Contains(i.GameId))
                .Select(i => i.GameId)
                .Distinct()
                .CountAsync(cancellationToken);

        var gamesWithOdds = gameIds.Count == 0
            ? 0
            : await _db.GameOdds
                .Where(o => gameIds.Contains(o.GameId))
                .Select(o => o.GameId)
                .Distinct()
                .CountAsync(cancellationToken);

        var playerCount = await _db.PlayerTeamSeasons
            .Where(pts => pts.Season == season)
            .Select(pts => pts.PlayerId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new SeasonCoverage
        {
            Season = season,
            SeasonType = seasonType,
            Week = week,
            ExpectedGames = NflSeasonSchedule.GetExpectedGamesForWeek(seasonType, season, week),
            ActualGames = games.Count,
            GamesWithPlayerStats = gamesWithPlayerStats,
            GamesWithTeamStats = gamesWithTeamStats,
            GamesWithInjuries = gamesWithInjuries,
            GamesWithOdds = gamesWithOdds,
            PlayerCount = playerCount,
            LastVerifiedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public async Task<IReadOnlyList<SeasonCoverage>> GetAsync(
        int? season = null,
        NflSeasonType? seasonType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.SeasonCoverages.AsQueryable();

        if (season.HasValue)
            query = query.Where(c => c.Season == season.Value);

        if (seasonType.HasValue)
            query = query.Where(c => c.SeasonType == seasonType.Value);

        return await query
            .OrderByDescending(c => c.Season)
            .ThenBy(c => c.SeasonType)
            .ThenBy(c => c.Week)
            .ToListAsync(cancellationToken);
    }

    private async Task UpsertAsync(SeasonCoverage snapshot, CancellationToken cancellationToken)
    {
        var existing = await _db.SeasonCoverages
            .FirstOrDefaultAsync(c =>
                c.Season == snapshot.Season &&
                c.SeasonType == snapshot.SeasonType &&
                c.Week == snapshot.Week,
                cancellationToken);

        if (existing is null)
        {
            _db.SeasonCoverages.Add(snapshot);
        }
        else
        {
            existing.ExpectedGames = snapshot.ExpectedGames;
            existing.ActualGames = snapshot.ActualGames;
            existing.GamesWithPlayerStats = snapshot.GamesWithPlayerStats;
            existing.GamesWithTeamStats = snapshot.GamesWithTeamStats;
            existing.GamesWithInjuries = snapshot.GamesWithInjuries;
            existing.GamesWithOdds = snapshot.GamesWithOdds;
            existing.PlayerCount = snapshot.PlayerCount;
            existing.LastVerifiedAt = snapshot.LastVerifiedAt;
            existing.UpdatedAt = snapshot.UpdatedAt;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
