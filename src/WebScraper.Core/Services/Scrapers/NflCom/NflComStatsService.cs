using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.NflCom;

public class NflComStatsService : BaseApiService, IStatsScraperService
{
    private readonly IStatsRepository _statsRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IGameRepository _gameRepository;
    private readonly ITeamRepository _teamRepository;

    public NflComStatsService(
        HttpClient httpClient,
        ILogger<NflComStatsService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        IStatsRepository statsRepository,
        IPlayerRepository playerRepository,
        IGameRepository gameRepository,
        ITeamRepository teamRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _statsRepository = statsRepository;
        _playerRepository = playerRepository;
        _gameRepository = gameRepository;
        _teamRepository = teamRepository;
    }

    public async Task<ScrapeResult> ScrapePlayerStatsAsync(int season, int week)
    {
        _logger.LogInformation("Starting player stats scrape for season {Season} week {Week} from NFL.com API",
            season, week);

        var games = await _gameRepository.GetByWeekAsync(season, week);
        var gamesList = games.ToList();

        if (!gamesList.Any())
        {
            _logger.LogWarning("No games found for season {Season} week {Week}. Scrape games first.", season, week);
            return ScrapeResult.Failed($"No games found for season {season} week {week}. Scrape games first.");
        }

        int totalStats = 0;
        foreach (var game in gamesList)
        {
            var count = await ScrapeGameStatsAsync(game, season, week);
            totalStats += count;
        }

        _logger.LogInformation(
            "Player stats scrape complete for season {Season} week {Week}. {Count} stat lines processed",
            season, week, totalStats);
        return ScrapeResult.Succeeded(totalStats, $"{totalStats} stat lines processed for season {season} week {week} from NFL.com API");
    }

    private async Task<int> ScrapeGameStatsAsync(Game game, int season, int week)
    {
        var homeTeam = game.HomeTeam ?? await _teamRepository.GetByIdAsync(game.HomeTeamId);
        if (homeTeam == null)
        {
            _logger.LogWarning("Home team not found for game {GameId}", game.Id);
            return 0;
        }

        var gameDetailId = NflComGameService.GetGameDetailId(season, week, homeTeam.Abbreviation);
        if (gameDetailId == null)
        {
            _logger.LogWarning(
                "No NFL.com gameDetailId found for game {GameId} (season {Season}, week {Week}, home {HomeAbbr}). " +
                "Scrape games first to populate game detail IDs.",
                game.Id, season, week, homeTeam.Abbreviation);
            return 0;
        }

        var response = await FetchJsonAsync<NflComGameStatsResponse>($"/games/{gameDetailId}/stats");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch stats for game {GameDetailId}", gameDetailId);
            return 0;
        }

        int count = 0;

        if (response.HomeTeamStats != null)
        {
            count += await ProcessTeamStatsAsync(response.HomeTeamStats, game.Id);
        }

        if (response.AwayTeamStats != null)
        {
            count += await ProcessTeamStatsAsync(response.AwayTeamStats, game.Id);
        }

        return count;
    }

    private async Task<int> ProcessTeamStatsAsync(NflComTeamStats teamStats, int gameId)
    {
        int count = 0;
        foreach (var dto in teamStats.PlayerStats)
        {
            if (string.IsNullOrEmpty(dto.DisplayName)) continue;
            if (!HasStats(dto)) continue;

            var player = await _playerRepository.GetByNameAsync(dto.DisplayName);
            if (player == null)
            {
                _logger.LogDebug("Player not found in database: {PlayerName}. Skipping.", dto.DisplayName);
                continue;
            }

            var stats = MapToStats(dto, player.Id, gameId);
            await _statsRepository.UpsertAsync(stats);
            count++;
        }

        return count;
    }

    private static bool HasStats(NflComPlayerStats dto)
    {
        return (dto.Passing?.Attempts ?? 0) > 0
            || (dto.Rushing?.Attempts ?? 0) > 0
            || (dto.Receiving?.Receptions ?? 0) > 0;
    }

    private static PlayerGameStats MapToStats(NflComPlayerStats dto, int playerId, int gameId)
    {
        return new PlayerGameStats
        {
            PlayerId = playerId,
            GameId = gameId,
            PassCompletions = dto.Passing?.Completions ?? 0,
            PassAttempts = dto.Passing?.Attempts ?? 0,
            PassYards = dto.Passing?.Yards ?? 0,
            PassTouchdowns = dto.Passing?.Touchdowns ?? 0,
            Interceptions = dto.Passing?.Interceptions ?? 0,
            RushAttempts = dto.Rushing?.Attempts ?? 0,
            RushYards = dto.Rushing?.Yards ?? 0,
            RushTouchdowns = dto.Rushing?.Touchdowns ?? 0,
            Receptions = dto.Receiving?.Receptions ?? 0,
            ReceivingYards = dto.Receiving?.Yards ?? 0,
            ReceivingTouchdowns = dto.Receiving?.Touchdowns ?? 0
        };
    }
}
