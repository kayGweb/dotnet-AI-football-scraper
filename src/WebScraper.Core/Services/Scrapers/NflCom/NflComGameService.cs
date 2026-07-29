using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.NflCom;

public class NflComGameService : BaseApiService, IGameScraperService
{
    private readonly IGameRepository _gameRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ITeamSeasonRepository _teamSeasonRepository;

    // Store gameDetailIds for stats lookups within the same session.
    // Key: "season:week:homeTeamAbbr", Value: gameDetailId
    private static readonly Dictionary<string, string> GameDetailIdLookup = new();

    public NflComGameService(
        HttpClient httpClient,
        ILogger<NflComGameService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        IGameRepository gameRepository,
        ITeamRepository teamRepository,
        ITeamSeasonRepository teamSeasonRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _gameRepository = gameRepository;
        _teamRepository = teamRepository;
        _teamSeasonRepository = teamSeasonRepository;
    }

    public async Task<ScrapeResult> ScrapeGamesAsync(int season, NflSeasonType seasonType = NflSeasonType.Regular)
    {
        if (seasonType != NflSeasonType.Regular)
            return ScrapeResult.Succeeded(0, $"NFL.com only supports regular season (requested {seasonType})");
        _logger.LogInformation("Starting games scrape for season {Season} from NFL.com API", season);

        int totalCount = 0;
        for (int week = 1; week <= 18; week++)
        {
            var count = await ScrapeWeekAsync(season, week);
            totalCount += count;
        }

        _logger.LogInformation("Games scrape complete for season {Season}. {Count} games processed", season, totalCount);
        return ScrapeResult.Succeeded(totalCount, $"{totalCount} games processed for season {season} from NFL.com API");
    }

    public async Task<ScrapeResult> ScrapeGamesAsync(int season, int week, NflSeasonType seasonType = NflSeasonType.Regular)
    {
        if (seasonType != NflSeasonType.Regular)
            return ScrapeResult.Succeeded(0, $"NFL.com only supports regular season (requested {seasonType})");
        _logger.LogInformation("Starting games scrape for season {Season} week {Week} from NFL.com API", season, week);

        var count = await ScrapeWeekAsync(season, week);

        _logger.LogInformation("Games scrape complete for season {Season} week {Week}. {Count} games processed",
            season, week, count);
        return ScrapeResult.Succeeded(count, $"{count} games processed for season {season} week {week} from NFL.com API");
    }

    private async Task<int> ScrapeWeekAsync(int season, int week)
    {
        var url = $"/games?season={season}&seasonType=REG&week={week}";
        var response = await FetchJsonAsync<NflComGamesResponse>(url);
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch games for season {Season} week {Week} from NFL.com API",
                season, week);
            return 0;
        }

        int count = 0;
        foreach (var dto in response.Games)
        {
            var game = await MapToGameAsync(dto, season);
            if (game != null)
            {
                await _gameRepository.UpsertAsync(game);
                count++;
            }
        }

        return count;
    }

    private async Task<Game?> MapToGameAsync(NflComGame dto, int season)
    {
        try
        {
            var homeTeam = await _teamRepository.GetByAbbreviationAsync(dto.HomeTeam.Abbreviation);
            var awayTeam = await _teamRepository.GetByAbbreviationAsync(dto.AwayTeam.Abbreviation);

            if (homeTeam == null || awayTeam == null)
            {
                _logger.LogDebug("Could not find teams: home={HomeTeam}, away={AwayTeam}",
                    dto.HomeTeam.Abbreviation, dto.AwayTeam.Abbreviation);
                return null;
            }

            var homeTeamSeason = await _teamSeasonRepository.EnsureFromTeamAsync(homeTeam, season);
            var awayTeamSeason = await _teamSeasonRepository.EnsureFromTeamAsync(awayTeam, season);

            DateTime gameDate = DateTime.MinValue;
            if (!string.IsNullOrEmpty(dto.GameDate))
            {
                DateTime.TryParse(dto.GameDate, out gameDate);
            }

            if (!string.IsNullOrEmpty(dto.GameDetailId))
            {
                var lookupKey = $"{season}:{dto.Week}:{dto.HomeTeam.Abbreviation}";
                GameDetailIdLookup[lookupKey] = dto.GameDetailId;
            }

            return new Game
            {
                Season = season,
                SeasonType = NflSeasonType.Regular,
                Week = dto.Week,
                GameDate = gameDate,
                HomeTeamSeasonId = homeTeamSeason.Id,
                AwayTeamSeasonId = awayTeamSeason.Id,
                HomeScore = dto.HomeTeamScore?.PointTotal,
                AwayScore = dto.AwayTeamScore?.PointTotal
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to map NFL.com game {GameDetailId}", dto.GameDetailId);
            return null;
        }
    }

    /// <summary>
    /// Gets the NFL.com gameDetailId for a given season, week, and home team abbreviation.
    /// Used by NflComStatsService to fetch game stats.
    /// </summary>
    internal static string? GetGameDetailId(int season, int week, string homeTeamAbbr)
    {
        var key = $"{season}:{week}:{homeTeamAbbr}";
        return GameDetailIdLookup.GetValueOrDefault(key);
    }
}
