using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.ApiSports;

public class ApiSportsGameService : ApiSportsServiceBase, IGameScraperService
{
    private readonly IGameRepository _gameRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ITeamSeasonRepository _teamSeasonRepository;
    private readonly IVenueRepository _venueRepository;

    public ApiSportsGameService(
        HttpClient httpClient,
        ILogger<ApiSportsGameService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        IGameRepository gameRepository,
        ITeamRepository teamRepository,
        ITeamSeasonRepository teamSeasonRepository,
        IVenueRepository venueRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _gameRepository = gameRepository;
        _teamRepository = teamRepository;
        _teamSeasonRepository = teamSeasonRepository;
        _venueRepository = venueRepository;
    }

    public async Task<ScrapeResult> ScrapeGamesAsync(int season, NflSeasonType seasonType = NflSeasonType.Regular)
    {
        _logger.LogInformation(
            "Starting games scrape for season {Season} ({SeasonType}) from api-sports",
            season, seasonType);

        var (items, error) = await FetchEnvelopeAsync<ApiSportsGameItem>($"games?league=1&season={season}");
        if (items == null)
            return ScrapeResult.Failed($"api-sports error: {error}");

        var count = await ProcessGamesAsync(items, season, seasonType, week: null);

        _logger.LogInformation(
            "api-sports games scrape complete for season {Season}. {Count} games processed",
            season, count);
        return ScrapeResult.Succeeded(count, $"{count} games processed for season {season} from api-sports");
    }

    public async Task<ScrapeResult> ScrapeGamesAsync(int season, int week, NflSeasonType seasonType = NflSeasonType.Regular)
    {
        _logger.LogInformation(
            "Starting games scrape for season {Season} week {Week} ({SeasonType}) from api-sports",
            season, week, seasonType);

        var (items, error) = await FetchEnvelopeAsync<ApiSportsGameItem>($"games?league=1&season={season}");
        if (items == null)
            return ScrapeResult.Failed($"api-sports error: {error}");

        var count = await ProcessGamesAsync(items, season, seasonType, week);

        _logger.LogInformation(
            "api-sports games scrape complete for season {Season} week {Week}. {Count} games processed",
            season, week, count);
        return ScrapeResult.Succeeded(count, $"{count} games processed for season {season} week {week} from api-sports");
    }

    private async Task<int> ProcessGamesAsync(
        List<ApiSportsGameItem> items,
        int season,
        NflSeasonType seasonType,
        int? week)
    {
        var fetchedAt = DateTime.UtcNow;
        var count = 0;

        foreach (var item in items)
        {
            if (!ApiSportsMappings.IsNfl(item))
                continue;

            var parsedWeek = ApiSportsMappings.ParseWeek(item.Game.Stage, item.Game.Week);
            if (parsedWeek == null)
                continue;

            if (parsedWeek.Value.SeasonType != seasonType)
                continue;

            if (week.HasValue && parsedWeek.Value.Week != week.Value)
                continue;

            var game = await MapToGameAsync(item, season, parsedWeek.Value.SeasonType, parsedWeek.Value.Week, fetchedAt);
            if (game == null)
                continue;

            await _gameRepository.UpsertAsync(game);
            count++;
        }

        return count;
    }

    private async Task<Game?> MapToGameAsync(
        ApiSportsGameItem item,
        int season,
        NflSeasonType seasonType,
        int week,
        DateTime fetchedAt)
    {
        try
        {
            var homeAbbr = ApiSportsMappings.TeamIdToAbbreviation(item.Teams.Home.Id);
            var awayAbbr = ApiSportsMappings.TeamIdToAbbreviation(item.Teams.Away.Id);
            if (homeAbbr == null || awayAbbr == null)
            {
                _logger.LogWarning(
                    "Skipping api-sports game {GameId}: unknown team id (home={HomeId}, away={AwayId})",
                    item.Game.Id, item.Teams.Home.Id, item.Teams.Away.Id);
                return null;
            }

            var homeTeam = await _teamRepository.GetByAbbreviationAsync(homeAbbr);
            var awayTeam = await _teamRepository.GetByAbbreviationAsync(awayAbbr);
            if (homeTeam == null || awayTeam == null)
            {
                _logger.LogWarning(
                    "Skipping api-sports game {GameId}: team not in database (home={HomeAbbr}, away={AwayAbbr})",
                    item.Game.Id, homeAbbr, awayAbbr);
                return null;
            }

            var homeTeamSeason = await _teamSeasonRepository.EnsureFromTeamAsync(homeTeam, season);
            var awayTeamSeason = await _teamSeasonRepository.EnsureFromTeamAsync(awayTeam, season);

            var status = ApiSportsMappings.ParseStatus(item.Game.Status.Short);
            int? homeScore = status.IsScheduled ? null : item.Scores.Home.Total;
            int? awayScore = status.IsScheduled ? null : item.Scores.Away.Total;

            int? homeQ1 = status.IsScheduled ? null : item.Scores.Home.Quarter1;
            int? homeQ2 = status.IsScheduled ? null : item.Scores.Home.Quarter2;
            int? homeQ3 = status.IsScheduled ? null : item.Scores.Home.Quarter3;
            int? homeQ4 = status.IsScheduled ? null : item.Scores.Home.Quarter4;
            int? homeOt = status.IsScheduled ? null : item.Scores.Home.Overtime;
            int? awayQ1 = status.IsScheduled ? null : item.Scores.Away.Quarter1;
            int? awayQ2 = status.IsScheduled ? null : item.Scores.Away.Quarter2;
            int? awayQ3 = status.IsScheduled ? null : item.Scores.Away.Quarter3;
            int? awayQ4 = status.IsScheduled ? null : item.Scores.Away.Quarter4;
            int? awayOt = status.IsScheduled ? null : item.Scores.Away.Overtime;

            bool? homeWinner = null;
            if (status.IsFinal && homeScore.HasValue && awayScore.HasValue)
                homeWinner = homeScore.Value > awayScore.Value;

            int? venueId = null;
            var venueName = item.Game.Venue?.Name;
            if (!string.IsNullOrWhiteSpace(venueName))
            {
                var venue = new Venue
                {
                    EspnId = venueName,
                    Name = venueName,
                    City = item.Game.Venue?.City ?? string.Empty,
                    State = string.Empty,
                    Country = "USA"
                };
                await _venueRepository.UpsertAsync(venue);
                var savedVenue = await _venueRepository.GetByEspnIdAsync(venueName);
                venueId = savedVenue?.Id;
            }

            return new Game
            {
                Season = season,
                SeasonType = seasonType,
                Week = week,
                GameDate = DateTimeOffset.FromUnixTimeSeconds(item.Game.Date.Timestamp).UtcDateTime,
                HomeTeamSeasonId = homeTeamSeason.Id,
                AwayTeamSeasonId = awayTeamSeason.Id,
                HomeScore = homeScore,
                AwayScore = awayScore,
                HomeQ1 = homeQ1,
                HomeQ2 = homeQ2,
                HomeQ3 = homeQ3,
                HomeQ4 = homeQ4,
                HomeOT = homeOt,
                AwayQ1 = awayQ1,
                AwayQ2 = awayQ2,
                AwayQ3 = awayQ3,
                AwayQ4 = awayQ4,
                AwayOT = awayOt,
                VenueId = venueId,
                GameStatus = status.GameStatus,
                HomeWinner = homeWinner,
                DataSource = "ApiSports",
                DataSourceRecordId = item.Game.Id.ToString(),
                DataSourceFetchedAt = fetchedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to map api-sports game {GameId}", item.Game.Id);
            return null;
        }
    }
}
