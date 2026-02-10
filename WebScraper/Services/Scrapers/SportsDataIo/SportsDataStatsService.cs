using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.SportsDataIo;

public class SportsDataStatsService : BaseApiService, IStatsScraperService
{
    private readonly IStatsRepository _statsRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IGameRepository _gameRepository;

    public SportsDataStatsService(
        HttpClient httpClient,
        ILogger<SportsDataStatsService> logger,
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

    public async Task ScrapePlayerStatsAsync(int season, int week)
    {
        _logger.LogInformation("Starting player stats scrape for season {Season} week {Week} from SportsData.io API",
            season, week);

        // SportsData.io returns all player stats for an entire week in one call
        var statsList = await FetchJsonAsync<List<SportsDataPlayerStatsDto>>(
            $"/stats/json/PlayerGameStatsByWeek/{season}/{week}");

        if (statsList == null)
        {
            _logger.LogWarning("Failed to fetch player stats for season {Season} week {Week} from SportsData.io API",
                season, week);
            return;
        }

        // Load games for this week to resolve GameId
        var games = (await _gameRepository.GetByWeekAsync(season, week)).ToList();
        if (!games.Any())
        {
            _logger.LogWarning("No games found for season {Season} week {Week}. Scrape games first.", season, week);
            return;
        }

        int count = 0;
        foreach (var dto in statsList)
        {
            // Skip players with no meaningful stats
            if (!HasStats(dto)) continue;

            var player = await _playerRepository.GetByNameAsync(dto.Name);
            if (player == null)
            {
                _logger.LogDebug("Player not found in database: {PlayerName}. Skipping.", dto.Name);
                continue;
            }

            // Find the game this player participated in
            var game = FindGameForPlayer(games, dto.Team);
            if (game == null)
            {
                _logger.LogDebug("Could not find game for player {PlayerName} (team {Team})", dto.Name, dto.Team);
                continue;
            }

            var stats = MapToStats(dto, player.Id, game.Id);
            await _statsRepository.UpsertAsync(stats);
            count++;
        }

        _logger.LogInformation(
            "Player stats scrape complete for season {Season} week {Week}. {Count} stat lines processed",
            season, week, count);
    }

    private static bool HasStats(SportsDataPlayerStatsDto dto)
    {
        return dto.PassingAttempts > 0
            || dto.RushingAttempts > 0
            || dto.Receptions > 0;
    }

    private static Game? FindGameForPlayer(List<Game> games, string? teamAbbr)
    {
        if (string.IsNullOrEmpty(teamAbbr)) return null;

        return games.FirstOrDefault(g =>
            (g.HomeTeam?.Abbreviation?.Equals(teamAbbr, StringComparison.OrdinalIgnoreCase) ?? false)
            || (g.AwayTeam?.Abbreviation?.Equals(teamAbbr, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private static PlayerGameStats MapToStats(SportsDataPlayerStatsDto dto, int playerId, int gameId)
    {
        return new PlayerGameStats
        {
            PlayerId = playerId,
            GameId = gameId,
            PassCompletions = dto.PassingCompletions,
            PassAttempts = dto.PassingAttempts,
            PassYards = dto.PassingYards,
            PassTouchdowns = dto.PassingTouchdowns,
            Interceptions = dto.PassingInterceptions,
            RushAttempts = dto.RushingAttempts,
            RushYards = dto.RushingYards,
            RushTouchdowns = dto.RushingTouchdowns,
            Receptions = dto.Receptions,
            ReceivingYards = dto.ReceivingYards,
            ReceivingTouchdowns = dto.ReceivingTouchdowns
        };
    }
}
