using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.Espn;

public class EspnGameService : BaseApiService, IGameScraperService
{
    private readonly IGameRepository _gameRepository;
    private readonly ITeamRepository _teamRepository;

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
        ITeamRepository teamRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _gameRepository = gameRepository;
        _teamRepository = teamRepository;
    }

    public async Task<ScrapeResult> ScrapeGamesAsync(int season)
    {
        _logger.LogInformation("Starting games scrape for season {Season} from ESPN API", season);

        // Scrape all 18 regular season weeks
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

            // Parse date
            DateTime gameDate = DateTime.MinValue;
            if (!string.IsNullOrEmpty(espnEvent.Date))
            {
                DateTime.TryParse(espnEvent.Date, out gameDate);
            }

            // Parse scores
            int? homeScore = int.TryParse(homeCompetitor.Score, out var hs) ? hs : null;
            int? awayScore = int.TryParse(awayCompetitor.Score, out var aws) ? aws : null;

            // Store event ID for stats scraping
            var lookupKey = $"{season}:{week}:{homeAbbr}";
            EventIdLookup[lookupKey] = espnEvent.Id;

            return new Game
            {
                Season = season,
                Week = week,
                GameDate = gameDate,
                HomeTeamId = homeTeam.Id,
                AwayTeamId = awayTeam.Id,
                HomeScore = homeScore,
                AwayScore = awayScore
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to map ESPN event {EventId} to game", espnEvent.Id);
            return null;
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
