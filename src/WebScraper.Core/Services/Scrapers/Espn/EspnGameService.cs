using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Services.Coverage;

namespace WebScraper.Services.Scrapers.Espn;

public class EspnGameService : BaseApiService, IGameScraperService
{
    private readonly IGameRepository _gameRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ITeamSeasonRepository _teamSeasonRepository;
    private readonly IFranchiseRepository _franchiseRepository;
    private readonly IVenueRepository _venueRepository;
    private readonly IApiLinkRepository _apiLinkRepository;

    // Key: "season:seasonType:week:homeTeamAbbr", Value: ESPN event ID
    private static readonly Dictionary<string, string> EventIdLookup = new();
    private static readonly HashSet<string> PopulatedWeeks = new();

    public EspnGameService(
        HttpClient httpClient,
        ILogger<EspnGameService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        IGameRepository gameRepository,
        ITeamRepository teamRepository,
        ITeamSeasonRepository teamSeasonRepository,
        IFranchiseRepository franchiseRepository,
        IVenueRepository venueRepository,
        IApiLinkRepository apiLinkRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _gameRepository = gameRepository;
        _teamRepository = teamRepository;
        _teamSeasonRepository = teamSeasonRepository;
        _franchiseRepository = franchiseRepository;
        _venueRepository = venueRepository;
        _apiLinkRepository = apiLinkRepository;
    }

    public async Task<ScrapeResult> ScrapeGamesAsync(int season, NflSeasonType seasonType = NflSeasonType.Regular)
    {
        _logger.LogInformation(
            "Starting games scrape for season {Season} type {SeasonType} from ESPN API",
            season, seasonType);

        var totalCount = 0;
        var weeks = NflSeasonSchedule.GetScoreboardWeeks(seasonType, season);
        for (var week = 1; week <= weeks; week++)
            totalCount += await ScrapeWeekAsync(season, week, seasonType);

        _logger.LogInformation(
            "Games scrape complete for season {Season} type {SeasonType}. {Count} games processed",
            season, seasonType, totalCount);
        return ScrapeResult.Succeeded(totalCount,
            $"{totalCount} games processed for season {season} ({seasonType}) from ESPN API");
    }

    public async Task<ScrapeResult> ScrapeGamesAsync(int season, int week, NflSeasonType seasonType = NflSeasonType.Regular)
    {
        _logger.LogInformation(
            "Starting games scrape for season {Season} week {Week} type {SeasonType} from ESPN API",
            season, week, seasonType);

        var count = await ScrapeWeekAsync(season, week, seasonType);

        _logger.LogInformation(
            "Games scrape complete for season {Season} week {Week} type {SeasonType}. {Count} games processed",
            season, week, seasonType, count);
        return ScrapeResult.Succeeded(count,
            $"{count} games processed for season {season} week {week} ({seasonType}) from ESPN API");
    }

    private async Task<int> ScrapeWeekAsync(int season, int week, NflSeasonType seasonType)
    {
        var url = $"/scoreboard?dates={season}&week={week}&seasontype={(int)seasonType}";
        var response = await FetchJsonAsync<EspnScoreboardResponse>(url);
        if (response == null)
        {
            _logger.LogWarning(
                "Failed to fetch scoreboard for season {Season} week {Week} type {SeasonType}",
                season, week, seasonType);
            return 0;
        }

        var count = 0;
        foreach (var espnEvent in response.Events)
        {
            var game = await MapToGameAsync(espnEvent, season, week, seasonType);
            if (game != null)
            {
                await _gameRepository.UpsertAsync(game);
                count++;
            }
        }

        PopulatedWeeks.Add(WeekKey(season, seasonType, week));
        return count;
    }

    private async Task<Game?> MapToGameAsync(
        EspnEvent espnEvent, int season, int week, NflSeasonType seasonType)
    {
        try
        {
            var competition = espnEvent.Competitions.FirstOrDefault();
            if (competition == null) return null;

            var homeCompetitor = competition.Competitors.FirstOrDefault(c =>
                c.HomeAway.Equals("home", StringComparison.OrdinalIgnoreCase));
            var awayCompetitor = competition.Competitors.FirstOrDefault(c =>
                c.HomeAway.Equals("away", StringComparison.OrdinalIgnoreCase));

            if (homeCompetitor == null || awayCompetitor == null) return null;

            var homeAbbr = EspnMappings.ToNflAbbreviation(homeCompetitor.Team.Id, homeCompetitor.Team.Abbreviation);
            var awayAbbr = EspnMappings.ToNflAbbreviation(awayCompetitor.Team.Id, awayCompetitor.Team.Abbreviation);

            var homeTeamSeason = await EnsureTeamSeasonAsync(homeCompetitor, homeAbbr, season);
            var awayTeamSeason = await EnsureTeamSeasonAsync(awayCompetitor, awayAbbr, season);

            if (homeTeamSeason == null || awayTeamSeason == null)
            {
                _logger.LogDebug("Could not resolve team seasons: home={HomeAbbr}, away={AwayAbbr}", homeAbbr, awayAbbr);
                return null;
            }

            DateTime gameDate = DateTime.MinValue;
            if (!string.IsNullOrEmpty(espnEvent.Date))
                DateTime.TryParse(espnEvent.Date, out gameDate);

            int? homeScore = int.TryParse(homeCompetitor.Score, out var hs) ? hs : null;
            int? awayScore = int.TryParse(awayCompetitor.Score, out var aws) ? aws : null;

            EventIdLookup[EventLookupKey(season, seasonType, week, homeAbbr)] = espnEvent.Id;

            int? venueId = null;
            if (competition.Venue != null && !string.IsNullOrEmpty(competition.Venue.Id))
            {
                var venue = new Venue
                {
                    EspnId = competition.Venue.Id,
                    Name = competition.Venue.FullName,
                    City = competition.Venue.Address?.City ?? string.Empty,
                    State = competition.Venue.Address?.State ?? string.Empty,
                    Country = competition.Venue.Address?.Country ?? string.Empty,
                    IsGrass = competition.Venue.Grass,
                    IsIndoor = competition.Venue.Indoor
                };
                await _venueRepository.UpsertAsync(venue);
                var saved = await _venueRepository.GetByEspnIdAsync(competition.Venue.Id);
                venueId = saved?.Id;
            }

            var homeQuarters = ParseLinescores(homeCompetitor.Linescores);
            var awayQuarters = ParseLinescores(awayCompetitor.Linescores);
            var gameStatus = competition.Status?.Type?.Name;

            var game = new Game
            {
                Season = season,
                SeasonType = seasonType,
                Week = week,
                GameDate = gameDate,
                HomeTeamSeasonId = homeTeamSeason.Id,
                AwayTeamSeasonId = awayTeamSeason.Id,
                HomeScore = homeScore,
                AwayScore = awayScore,
                VenueId = venueId,
                Attendance = competition.Attendance,
                NeutralSite = competition.NeutralSite,
                EspnEventId = espnEvent.Id,
                GameStatus = gameStatus,
                HomeWinner = homeCompetitor.Winner,
                HomeQ1 = homeQuarters[0],
                HomeQ2 = homeQuarters[1],
                HomeQ3 = homeQuarters[2],
                HomeQ4 = homeQuarters[3],
                HomeOT = homeQuarters[4],
                AwayQ1 = awayQuarters[0],
                AwayQ2 = awayQuarters[1],
                AwayQ3 = awayQuarters[2],
                AwayQ4 = awayQuarters[3],
                AwayOT = awayQuarters[4],
                BroadcastNetworks = FormatScoreboardBroadcasts(competition.Broadcasts)
            };

            await StoreApiLinkAsync(
                $"https://site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event={espnEvent.Id}",
                "summary", "boxscore", season, week, espnEvent.Id, null, homeTeamSeason.Id);

            return game;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to map ESPN event {EventId} to game", espnEvent.Id);
            return null;
        }
    }

    private async Task<TeamSeason?> EnsureTeamSeasonAsync(
        EspnCompetitor competitor, string abbreviation, int season)
    {
        var team = await _teamRepository.GetByAbbreviationAsync(abbreviation);
        if (team != null)
            return await _teamSeasonRepository.EnsureFromTeamAsync(team, season);

        var (conference, division) = EspnMappings.GetDivision(abbreviation);
        var franchise = await _franchiseRepository.GetOrCreateAsync(
            abbreviation, competitor.Team.Abbreviation);

        return await _teamSeasonRepository.UpsertAsync(new TeamSeason
        {
            FranchiseId = franchise.Id,
            Season = season,
            Name = competitor.Team.Abbreviation,
            Abbreviation = abbreviation,
            City = string.Empty,
            Conference = conference,
            Division = division,
        });
    }

    private static int?[] ParseLinescores(List<EspnLinescore>? linescores)
    {
        var result = new int?[5];
        if (linescores == null) return result;

        for (var i = 0; i < linescores.Count && i < 5; i++)
            result[i] = (int)linescores[i].Value;

        return result;
    }

    internal static string? FormatScoreboardBroadcasts(List<EspnScoreboardBroadcast>? broadcasts)
    {
        if (broadcasts == null || broadcasts.Count == 0)
            return null;

        var names = broadcasts
            .SelectMany(b => b.Names ?? [])
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return names.Count > 0 ? string.Join(", ", names) : null;
    }

    private async Task StoreApiLinkAsync(
        string url, string endpointType, string relationType,
        int season, int week, string espnEventId,
        int? gameId, int? teamSeasonId)
    {
        try
        {
            var apiLink = new ApiLink
            {
                Url = url,
                EndpointType = endpointType,
                RelationType = relationType,
                Season = season,
                Week = week,
                EspnEventId = espnEventId,
                GameId = gameId,
                TeamId = teamSeasonId,
                DiscoveredAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };
            await _apiLinkRepository.UpsertAsync(apiLink);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to store API link: {Url}", url);
        }
    }

    internal static string? GetEventId(int season, int week, string homeTeamAbbr, NflSeasonType seasonType = NflSeasonType.Regular)
    {
        return EventIdLookup.GetValueOrDefault(EventLookupKey(season, seasonType, week, homeTeamAbbr));
    }

    internal static void ClearEventIdCache()
    {
        EventIdLookup.Clear();
        PopulatedWeeks.Clear();
    }

    internal static bool HasEventIdsForWeek(int season, int week, NflSeasonType seasonType = NflSeasonType.Regular)
        => PopulatedWeeks.Contains(WeekKey(season, seasonType, week));

    internal static async Task PopulateEventIdsAsync(
        HttpClient httpClient,
        ILogger logger,
        RateLimiterService rateLimiter,
        int season,
        int week,
        NflSeasonType seasonType = NflSeasonType.Regular)
    {
        var weekKey = WeekKey(season, seasonType, week);
        if (PopulatedWeeks.Contains(weekKey))
            return;

        await rateLimiter.WaitAsync();

        var url = $"scoreboard?dates={season}&week={week}&seasontype={(int)seasonType}";
        logger.LogInformation(
            "Fetching ESPN scoreboard to populate event IDs for season {Season} week {Week} type {SeasonType}",
            season, week, seasonType);

        try
        {
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var scoreboard = System.Text.Json.JsonSerializer.Deserialize<EspnScoreboardResponse>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (scoreboard == null)
            {
                logger.LogWarning(
                    "Failed to deserialize ESPN scoreboard for season {Season} week {Week} type {SeasonType}",
                    season, week, seasonType);
                return;
            }

            var count = 0;
            foreach (var espnEvent in scoreboard.Events)
            {
                var competition = espnEvent.Competitions.FirstOrDefault();
                if (competition == null) continue;

                var homeCompetitor = competition.Competitors.FirstOrDefault(c =>
                    c.HomeAway.Equals("home", StringComparison.OrdinalIgnoreCase));
                if (homeCompetitor == null) continue;

                var homeAbbr = EspnMappings.ToNflAbbreviation(homeCompetitor.Team.Id, homeCompetitor.Team.Abbreviation);
                EventIdLookup[EventLookupKey(season, seasonType, week, homeAbbr)] = espnEvent.Id;
                count++;
            }

            PopulatedWeeks.Add(weekKey);
            logger.LogInformation(
                "Populated {Count} ESPN event IDs for season {Season} week {Week} type {SeasonType}",
                count, season, week, seasonType);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to fetch ESPN scoreboard for event ID population (season {Season} week {Week} type {SeasonType})",
                season, week, seasonType);
        }
    }

    private static string WeekKey(int season, NflSeasonType seasonType, int week)
        => $"{season}:{(int)seasonType}:{week}";

    private static string EventLookupKey(int season, NflSeasonType seasonType, int week, string homeAbbr)
        => $"{season}:{(int)seasonType}:{week}:{homeAbbr}";
}
