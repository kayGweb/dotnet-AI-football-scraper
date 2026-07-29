using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.Espn;

/// <summary>
/// Shared logic for persisting ESPN pickcenter odds with Opening/Current/Closing semantics.
/// </summary>
internal static class EspnOddsCapture
{
    public static async Task<int> SavePickcenterAsync(
        IGameOddsRepository repository,
        int gameId,
        IReadOnlyList<EspnPickCenter> pickcenter,
        bool isFinal,
        IReadOnlyList<GameOdds>? existingOdds = null)
    {
        if (pickcenter.Count == 0)
            return 0;

        existingOdds ??= await repository.GetByGameAsync(gameId);
        var capturedAt = DateTime.UtcNow;
        var saved = 0;

        foreach (var pick in pickcenter)
        {
            var sportsbook = pick.Provider?.Name;
            if (string.IsNullOrWhiteSpace(sportsbook))
                sportsbook = "ESPN";

            var forBook = existingOdds.Where(o => o.Sportsbook == sportsbook).ToList();
            var hasOpening = forBook.Any(o => o.SnapshotType == OddsSnapshotType.Opening);
            var hasClosing = forBook.Any(o => o.SnapshotType == OddsSnapshotType.Closing);

            var snapshotType = ResolveSnapshotType(isFinal, hasOpening, hasClosing);
            if (snapshotType is null)
                continue;

            var odds = new GameOdds
            {
                GameId = gameId,
                Sportsbook = sportsbook,
                Spread = pick.Spread,
                OverUnder = pick.OverUnder,
                HomeMoneyline = pick.HomeTeamOdds?.MoneyLine,
                AwayMoneyline = pick.AwayTeamOdds?.MoneyLine,
                SnapshotType = snapshotType.Value,
                CapturedAt = capturedAt,
                Details = pick.Details,
                DataSource = "Espn",
                DataSourceFetchedAt = capturedAt,
            };

            if (snapshotType == OddsSnapshotType.Current)
            {
                var last = forBook.OrderByDescending(o => o.CapturedAt).FirstOrDefault();
                if (last != null && OddsValuesMatch(last, odds))
                    continue;
            }

            await repository.AddSnapshotAsync(odds);
            saved++;
        }

        return saved;
    }

    public static OddsSnapshotType? ResolveSnapshotType(bool isFinal, bool hasOpening, bool hasClosing)
    {
        if (isFinal)
            return hasClosing ? null : OddsSnapshotType.Closing;

        if (!hasOpening)
            return OddsSnapshotType.Opening;

        return OddsSnapshotType.Current;
    }

    public static bool IsGameFinal(Game game)
    {
        if (game.GameStatus?.Contains("final", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        return game.HomeScore.HasValue && game.AwayScore.HasValue
            && game.GameStatus?.Contains("progress", StringComparison.OrdinalIgnoreCase) != true
            && game.GameStatus?.Contains("scheduled", StringComparison.OrdinalIgnoreCase) != true;
    }

    private static bool OddsValuesMatch(GameOdds existing, GameOdds incoming) =>
        existing.Spread == incoming.Spread
        && existing.OverUnder == incoming.OverUnder
        && existing.HomeMoneyline == incoming.HomeMoneyline
        && existing.AwayMoneyline == incoming.AwayMoneyline;
}
