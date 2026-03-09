using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.Espn;

public class EspnGameService : BaseApiService, IGameScraperService
{
    private readonly IGameRepository _gameRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IVenueRepository _venueRepository;
    private readonly IApiLinkRepository _apiLinkRepository;

    // Key: "season:week:homeTeamAbbr", Value: ESPN event ID
    private static readonly Dictionary<string, string> EventIdLookup = new();
    // Tracks which season:week combos have been fetched from the API
    private static readonly HashSet<string> PopulatedWeeks = new();

    public EspnGameService(
        HttpClient httpClient,
        ILogger<EspnGameService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        IGameRepository gameRepository,
        ITeamRepository teamRepository,
        IVenueRepository venueRepository,
        IApiLinkRepository apiLinkRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _gameRepository = gameRepository;
        _teamRepository = teamRepository;
        _venueRepository = venueRepository;
        _apiLinkRepository = apiLinkRepository;
    }

    public async Task<ScrapeResult> ScrapeGamesAsync(int season)
    {
        _logger.LogInformation("Starting games scrape for season {Season} from ESPN API", season);

        int totalCount = 0;
        for (int week = 1; week <= 18; week++)
        {
            var count = await ScrapeWeekAsync(season, week);
            totalCount += count;
        }

        _logger.LogInformation("Games scrape complete for season {Season}. {Count} games processed", season, totalCount);
        return ScrapeResult.Succeeded(totalCount, $"{totalCount} games processed for season {season} from ESPN API");
    }

    public async Task<ScrapeResult> ScrapeGamesAsync(int season, int week)
    {
        _logger.LogInformation("Starting games scrape for season {Season} week {Week} from ESPN API", season, week);

        var count = await ScrapeWeekAsync(season, week);

        _logger.LogInformation("Games scrape complete for season {Season} week {Week}. {Count} games processed", season, week, count);
        return ScrapeResult.Succeeded(count, $"{count} games processed for season {season} week {week} from ESPN API");
    }

    private async Task<int> ScrapeWeekAsync(int season, int week)
    {
        var url = $"/scoreboard?dates={season}&week={week}&seasontype=2";
        var response = await FetchJsonAsync<EspnScoreboardResponse>(url);
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch scoreboard for season {Season} week {Week} from ESPN API", season, week);
            return 0;
        }

        int count = 0;
        foreach (var espnEvent in response.Events)
        {
            var game = await MapToGameAsync(espnEvent, season, week);
            if (game != null)
            {
                await _gameRepository.UpsertAsync(game);
                count++;
            }
        }

        PopulatedWeeks.Add($"{season}:{week}");
        return count;
    }

    private async Task<Game?> MapToGameAsync(EspnEvent espnEvent, int season, int week)
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

            var homeTeam = await _teamRepository.GetByAbbreviationAsync(homeAbbr);
            var awayTeam = await _teamRepository.GetByAbbreviationAsync(awayAbbr);

            if (homeTeam == null || awayTeam == null)
            {
                _logger.LogDebug("Could not find teams: home={HomeAbbr}, away={AwayAbbr}", homeAbbr, awayAbbr);
                return null;
            }

            DateTime gameDate = DateTime.MinValue;
            if (!string.IsNullOrEmpty(espnEvent.Date))
            {
                DateTime.TryParse(espnEvent.Date, out gameDate);
            }

            int? homeScore = int.TryParse(homeCompetitor.Score, out var hs) ? hs : null;
            int? awayScore = int.TryParse(awayCompetitor.Score, out var aws) ? aws : null;

            // Store event ID for stats scraping
            var lookupKey = $"{season}:{week}:{homeAbbr}";
            EventIdLookup[lookupKey] = espnEvent.Id;

            // Upsert venue if present
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

            // Parse quarter scores from linescores
            int?[] homeQuarters = ParseLinescores(homeCompetitor.Linescores);
            int?[] awayQuarters = ParseLinescores(awayCompetitor.Linescores);

            // Game status
            string? gameStatus = competition.Status?.Type?.Name;

            var game = new Game
            {
                Season = season,
                Week = week,
                GameDate = gameDate,
                HomeTeamId = homeTeam.Id,
                AwayTeamId = awayTeam.Id,
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
                AwayOT = awayQuarters[4]
            };

            // Store scoreboard API link
            await StoreApiLinkAsync(
                $"https://site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event={espnEvent.Id}",
                "summary", "boxscore", season, week, espnEvent.Id, null, homeTeam.Id);

            return game;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to map ESPN event {EventId} to game", espnEvent.Id);
            return null;
        }
    }

    private static int?[] ParseLinescores(List<EspnLinescore>? linescores)
    {
        var result = new int?[5]; // Q1, Q2, Q3, Q4, OT
        if (linescores == null) return result;

        for (int i = 0; i < linescores.Count && i < 5; i++)
        {
            result[i] = (int)linescores[i].Value;
        }
        return result;
    }

    private async Task StoreApiLinkAsync(
        string url, string endpointType, string relationType,
        int season, int week, string espnEventId,
        int? gameId, int? teamId)
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
                TeamId = teamId,
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

    /// <summary>
    /// Gets the ESPN event ID for a given season, week, and home team abbreviation.
    /// Used by EspnStatsService to fetch box scores.
    /// </summary>
    internal static string? GetEventId(int season, int week, string homeTeamAbbr)
    {
        var key = $"{season}:{week}:{homeTeamAbbr}";
        return EventIdLookup.GetValueOrDefault(key);
    }

    /// <summary>
    /// Clears all cached event IDs and populated-week markers. Used by tests.
    /// </summary>
    internal static void ClearEventIdCache()
    {
        EventIdLookup.Clear();
        PopulatedWeeks.Clear();
    }

    /// <summary>
    /// Returns true if event IDs have already been populated for this season/week
    /// (either via game scraping or an explicit <see cref="PopulateEventIdsAsync"/> call).
    /// </summary>
    internal static bool HasEventIdsForWeek(int season, int week)
    {
        return PopulatedWeeks.Contains($"{season}:{week}");
    }

    /// <summary>
    /// Fetches the ESPN scoreboard for the given season/week and populates the
    /// in-memory event ID cache. Does not write to the database — used by
    /// <see cref="EspnStatsService"/> when the cache is cold.
    /// </summary>
    internal static async Task PopulateEventIdsAsync(
        HttpClient httpClient,
        ILogger logger,
        RateLimiterService rateLimiter,
        int season,
        int week)
    {
        var weekKey = $"{season}:{week}";
        if (PopulatedWeeks.Contains(weekKey))
            return;

        await rateLimiter.WaitAsync();

        var url = $"scoreboard?dates={season}&week={week}&seasontype=2";
        logger.LogInformation("Fetching ESPN scoreboard to populate event IDs for season {Season} week {Week}", season, week);

        try
        {
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var scoreboard = System.Text.Json.JsonSerializer.Deserialize<EspnScoreboardResponse>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (scoreboard == null)
            {
                logger.LogWarning("Failed to deserialize ESPN scoreboard for season {Season} week {Week}", season, week);
                return;
            }

            int count = 0;
            foreach (var espnEvent in scoreboard.Events)
            {
                var competition = espnEvent.Competitions.FirstOrDefault();
                if (competition == null) continue;

                var homeCompetitor = competition.Competitors.FirstOrDefault(c =>
                    c.HomeAway.Equals("home", StringComparison.OrdinalIgnoreCase));
                if (homeCompetitor == null) continue;

                var homeAbbr = EspnMappings.ToNflAbbreviation(homeCompetitor.Team.Id, homeCompetitor.Team.Abbreviation);
                var lookupKey = $"{season}:{week}:{homeAbbr}";
                EventIdLookup[lookupKey] = espnEvent.Id;
                count++;
            }

            PopulatedWeeks.Add(weekKey);
            logger.LogInformation("Populated {Count} ESPN event IDs for season {Season} week {Week}", count, season, week);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch ESPN scoreboard for event ID population (season {Season} week {Week})", season, week);
        }
    }
}
