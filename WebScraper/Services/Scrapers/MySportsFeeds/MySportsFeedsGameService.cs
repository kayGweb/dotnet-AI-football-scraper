using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.MySportsFeeds;

public class MySportsFeedsGameService : BaseApiService, IGameScraperService
{
    private readonly IGameRepository _gameRepository;
    private readonly ITeamRepository _teamRepository;

    public MySportsFeedsGameService(
        HttpClient httpClient,
        ILogger<MySportsFeedsGameService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        IGameRepository gameRepository,
        ITeamRepository teamRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _gameRepository = gameRepository;
        _teamRepository = teamRepository;
    }

    public async Task ScrapeGamesAsync(int season)
    {
        _logger.LogInformation("Starting games scrape for season {Season} from MySportsFeeds API", season);

        int totalCount = 0;
        for (int week = 1; week <= 18; week++)
        {
            var count = await ScrapeWeekAsync(season, week);
            totalCount += count;
        }

        _logger.LogInformation("Games scrape complete for season {Season}. {Count} games processed", season, totalCount);
    }

    public async Task ScrapeGamesAsync(int season, int week)
    {
        _logger.LogInformation("Starting games scrape for season {Season} week {Week} from MySportsFeeds API",
            season, week);

        var count = await ScrapeWeekAsync(season, week);

        _logger.LogInformation("Games scrape complete for season {Season} week {Week}. {Count} games processed",
            season, week, count);
    }

    private async Task<int> ScrapeWeekAsync(int season, int week)
    {
        var response = await FetchJsonAsync<MySportsFeedsGamesResponse>($"/{season}/games.json?week={week}");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch games for season {Season} week {Week} from MySportsFeeds API",
                season, week);
            return 0;
        }

        int count = 0;
        foreach (var wrapper in response.Games)
        {
            var game = await MapToGameAsync(wrapper.Schedule, season);
            if (game != null)
            {
                await _gameRepository.UpsertAsync(game);
                count++;
            }
        }

        return count;
    }

    private async Task<Game?> MapToGameAsync(MySportsFeedsSchedule schedule, int season)
    {
        try
        {
            var homeTeam = await _teamRepository.GetByAbbreviationAsync(schedule.HomeTeam.Abbreviation);
            var awayTeam = await _teamRepository.GetByAbbreviationAsync(schedule.AwayTeam.Abbreviation);

            if (homeTeam == null || awayTeam == null)
            {
                _logger.LogDebug("Could not find teams: home={HomeTeam}, away={AwayTeam}",
                    schedule.HomeTeam.Abbreviation, schedule.AwayTeam.Abbreviation);
                return null;
            }

            DateTime gameDate = DateTime.MinValue;
            if (!string.IsNullOrEmpty(schedule.StartTime))
            {
                DateTime.TryParse(schedule.StartTime, out gameDate);
            }

            return new Game
            {
                Season = season,
                Week = schedule.Week,
                GameDate = gameDate,
                HomeTeamId = homeTeam.Id,
                AwayTeamId = awayTeam.Id,
                HomeScore = schedule.Score?.HomeScoreTotal,
                AwayScore = schedule.Score?.AwayScoreTotal
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to map MySportsFeeds game {GameId}", schedule.Id);
            return null;
        }
    }
}
