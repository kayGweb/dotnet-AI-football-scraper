using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.SportsDataIo;

public class SportsDataGameService : BaseApiService, IGameScraperService
{
    private readonly IGameRepository _gameRepository;
    private readonly ITeamRepository _teamRepository;

    public SportsDataGameService(
        HttpClient httpClient,
        ILogger<SportsDataGameService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        IGameRepository gameRepository,
        ITeamRepository teamRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _gameRepository = gameRepository;
        _teamRepository = teamRepository;
    }

    public async Task<ScrapeResult> ScrapeGamesAsync(int season)
    {
        _logger.LogInformation("Starting games scrape for season {Season} from SportsData.io API", season);

        int totalCount = 0;
        for (int week = 1; week <= 18; week++)
        {
            var count = await ScrapeWeekAsync(season, week);
            totalCount += count;
        }

        _logger.LogInformation("Games scrape complete for season {Season}. {Count} games processed", season, totalCount);
        return ScrapeResult.Succeeded(totalCount, $"{totalCount} games processed for season {season} from SportsData.io API");
    }

    public async Task<ScrapeResult> ScrapeGamesAsync(int season, int week)
    {
        _logger.LogInformation("Starting games scrape for season {Season} week {Week} from SportsData.io API", season, week);

        var count = await ScrapeWeekAsync(season, week);

        _logger.LogInformation("Games scrape complete for season {Season} week {Week}. {Count} games processed",
            season, week, count);
        return ScrapeResult.Succeeded(count, $"{count} games processed for season {season} week {week} from SportsData.io API");
    }

    private async Task<int> ScrapeWeekAsync(int season, int week)
    {
        var games = await FetchJsonAsync<List<SportsDataGameDto>>($"/scores/json/ScoresByWeek/{season}/{week}");
        if (games == null)
        {
            _logger.LogWarning("Failed to fetch scores for season {Season} week {Week} from SportsData.io API",
                season, week);
            return 0;
        }

        int count = 0;
        foreach (var dto in games)
        {
            var game = await MapToGameAsync(dto, season, week);
            if (game != null)
            {
                await _gameRepository.UpsertAsync(game);
                count++;
            }
        }

        return count;
    }

    private async Task<Game?> MapToGameAsync(SportsDataGameDto dto, int season, int week)
    {
        try
        {
            var homeTeam = await _teamRepository.GetByAbbreviationAsync(dto.HomeTeam);
            var awayTeam = await _teamRepository.GetByAbbreviationAsync(dto.AwayTeam);

            if (homeTeam == null || awayTeam == null)
            {
                _logger.LogDebug("Could not find teams: home={HomeTeam}, away={AwayTeam}",
                    dto.HomeTeam, dto.AwayTeam);
                return null;
            }

            DateTime gameDate = DateTime.MinValue;
            if (!string.IsNullOrEmpty(dto.Date))
            {
                DateTime.TryParse(dto.Date, out gameDate);
            }

            return new Game
            {
                Season = season,
                Week = week,
                GameDate = gameDate,
                HomeTeamId = homeTeam.Id,
                AwayTeamId = awayTeam.Id,
                HomeScore = dto.HomeScore,
                AwayScore = dto.AwayScore
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to map SportsData.io game {GameKey}", dto.GameKey);
            return null;
        }
    }
}
