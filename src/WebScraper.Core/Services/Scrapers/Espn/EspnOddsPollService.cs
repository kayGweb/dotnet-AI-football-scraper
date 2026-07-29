using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebScraper.Data;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.Espn;

/// <summary>
/// Polls ESPN /summary pickcenter for upcoming and recently completed games to capture
/// opening, intraday, and closing lines (§5.1).
/// </summary>
public class EspnOddsPollService : BaseApiService, IOddsPollService
{
    private readonly AppDbContext _db;
    private readonly IGameOddsRepository _gameOddsRepository;
    private readonly OddsPollSettings _settings;

    public EspnOddsPollService(
        HttpClient httpClient,
        ILogger<EspnOddsPollService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        AppDbContext db,
        IGameOddsRepository gameOddsRepository,
        IOptions<OddsPollSettings> settings)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _db = db;
        _gameOddsRepository = gameOddsRepository;
        _settings = settings.Value;
    }

    public async Task<ScrapeResult> PollAsync(int? season = null, CancellationToken cancellationToken = default)
    {
        var games = await FindEligibleGamesAsync(season, cancellationToken);
        if (games.Count == 0)
            return ScrapeResult.Succeeded(0, "No eligible games for odds poll");

        var saved = 0;
        var failed = 0;

        foreach (var game in games)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var response = await FetchJsonAsync<EspnSummaryResponse>($"/summary?event={game.EspnEventId}");
                if (response?.Pickcenter == null || response.Pickcenter.Count == 0)
                    continue;

                var isFinal = EspnOddsCapture.IsGameFinal(game);
                saved += await EspnOddsCapture.SavePickcenterAsync(
                    _gameOddsRepository,
                    game.Id,
                    response.Pickcenter,
                    isFinal);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogDebug(ex, "Odds poll failed for game {GameId} event {EventId}", game.Id, game.EspnEventId);
            }
        }

        return ScrapeResult.Succeeded(
            saved,
            $"Odds poll complete: {saved} snapshots saved across {games.Count} games ({failed} fetch failures)");
    }

    private async Task<List<Game>> FindEligibleGamesAsync(int? season, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var windowStart = now.AddDays(-_settings.LookBackDays);
        var windowEnd = now.AddDays(_settings.LookAheadDays);

        var query = _db.Games
            .AsNoTracking()
            .Where(g => g.EspnEventId != null && g.GameDate >= windowStart && g.GameDate <= windowEnd);

        if (season.HasValue)
            query = query.Where(g => g.Season == season.Value);

        return await query
            .OrderBy(g => g.GameDate)
            .ToListAsync(cancellationToken);
    }
}
