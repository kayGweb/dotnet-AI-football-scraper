using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.MySportsFeeds;

public class MySportsFeedsStatsService : BaseApiService, IStatsScraperService
{
    private readonly IStatsRepository _statsRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IGameRepository _gameRepository;

    public MySportsFeedsStatsService(
        HttpClient httpClient,
        ILogger<MySportsFeedsStatsService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        IStatsRepository statsRepository,
        IPlayerRepository playerRepository,
        IGameRepository gameRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _statsRepository = statsRepository;
        _playerRepository = playerRepository;
        _gameRepository = gameRepository;
    }

    public async Task<ScrapeResult> ScrapePlayerStatsAsync(int season, int week)
    {
        _logger.LogInformation("Starting player stats scrape for season {Season} week {Week} from MySportsFeeds API",
            season, week);

        var response = await FetchJsonAsync<MySportsFeedsGameLogsResponse>(
            $"/{season}/week/{week}/player_gamelogs.json");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch player gamelogs for season {Season} week {Week} from MySportsFeeds API",
                season, week);
            return ScrapeResult.Failed($"Failed to fetch player gamelogs for season {season} week {week} from MySportsFeeds API");
        }

        // Load games for this week to resolve GameId
        var games = (await _gameRepository.GetByWeekAsync(season, week)).ToList();
        if (!games.Any())
        {
            _logger.LogWarning("No games found for season {Season} week {Week}. Scrape games first.", season, week);
            return ScrapeResult.Failed($"No games found for season {season} week {week}. Scrape games first.");
        }

        int count = 0;
        foreach (var gamelog in response.Gamelogs)
        {
            if (!HasStats(gamelog.Stats)) continue;

            // Concatenate first + last name
            var playerName = $"{gamelog.Player.FirstName} {gamelog.Player.LastName}".Trim();
            if (string.IsNullOrEmpty(playerName)) continue;

            var player = await _playerRepository.GetByNameAsync(playerName);
            if (player == null)
            {
                _logger.LogDebug("Player not found in database: {PlayerName}. Skipping.", playerName);
                continue;
            }

            // Find the game using the home/away team from the gamelog
            var game = FindGame(games, gamelog.Game);
            if (game == null)
            {
                _logger.LogDebug("Could not find matching game for player {PlayerName}", playerName);
                continue;
            }

            var stats = MapToStats(gamelog.Stats, player.Id, game.Id);
            await _statsRepository.UpsertAsync(stats);
            count++;
        }

        _logger.LogInformation(
            "Player stats scrape complete for season {Season} week {Week}. {Count} stat lines processed",
            season, week, count);
        return ScrapeResult.Succeeded(count, $"{count} stat lines processed for season {season} week {week} from MySportsFeeds API");
    }

    private static bool HasStats(MySportsFeedsStats stats)
    {
        return (stats.Passing?.PassAttempts ?? 0) > 0
            || (stats.Rushing?.RushAttempts ?? 0) > 0
            || (stats.Receiving?.Receptions ?? 0) > 0;
    }

    private static Game? FindGame(List<Game> games, MySportsFeedsGameLogGame gameLog)
    {
        var homeAbbr = gameLog.HomeTeam?.Abbreviation;
        var awayAbbr = gameLog.AwayTeam?.Abbreviation;

        if (!string.IsNullOrEmpty(homeAbbr) && !string.IsNullOrEmpty(awayAbbr))
        {
            return games.FirstOrDefault(g =>
                (g.HomeTeam?.Abbreviation?.Equals(homeAbbr, StringComparison.OrdinalIgnoreCase) ?? false)
                && (g.AwayTeam?.Abbreviation?.Equals(awayAbbr, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return null;
    }

    private static PlayerGameStats MapToStats(MySportsFeedsStats stats, int playerId, int gameId)
    {
        return new PlayerGameStats
        {
            PlayerId = playerId,
            GameId = gameId,
            PassCompletions = stats.Passing?.PassCompletions ?? 0,
            PassAttempts = stats.Passing?.PassAttempts ?? 0,
            PassYards = stats.Passing?.PassYards ?? 0,
            PassTouchdowns = stats.Passing?.PassTouchdowns ?? 0,
            Interceptions = stats.Passing?.Interceptions ?? 0,
            RushAttempts = stats.Rushing?.RushAttempts ?? 0,
            RushYards = stats.Rushing?.RushYards ?? 0,
            RushTouchdowns = stats.Rushing?.RushTouchdowns ?? 0,
            Receptions = stats.Receiving?.Receptions ?? 0,
            ReceivingYards = stats.Receiving?.ReceivingYards ?? 0,
            ReceivingTouchdowns = stats.Receiving?.ReceivingTouchdowns ?? 0
        };
    }
}
