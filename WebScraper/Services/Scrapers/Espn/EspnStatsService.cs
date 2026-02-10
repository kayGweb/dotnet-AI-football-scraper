using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.Espn;

public class EspnStatsService : BaseApiService, IStatsScraperService
{
    private readonly IStatsRepository _statsRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IGameRepository _gameRepository;
    private readonly ITeamRepository _teamRepository;

    public EspnStatsService(
        HttpClient httpClient,
        ILogger<EspnStatsService> logger,
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

    public async Task ScrapePlayerStatsAsync(int season, int week)
    {
        _logger.LogInformation("Starting player stats scrape for season {Season} week {Week} from ESPN API", season, week);

        var games = await _gameRepository.GetByWeekAsync(season, week);
        var gamesList = games.ToList();

        if (!gamesList.Any())
        {
            _logger.LogWarning("No games found for season {Season} week {Week}. Scrape games first.", season, week);
            return;
        }

        int totalStats = 0;
        foreach (var game in gamesList)
        {
            var count = await ScrapeGameStatsAsync(game, season, week);
            totalStats += count;
        }

        _logger.LogInformation("Player stats scrape complete for season {Season} week {Week}. {Count} stat lines processed",
            season, week, totalStats);
    }

    private async Task<int> ScrapeGameStatsAsync(Game game, int season, int week)
    {
        // Look up the home team abbreviation to find the ESPN event ID
        var homeTeam = game.HomeTeam ?? await _teamRepository.GetByIdAsync(game.HomeTeamId);
        if (homeTeam == null)
        {
            _logger.LogWarning("Home team not found for game {GameId}", game.Id);
            return 0;
        }

        var eventId = EspnGameService.GetEventId(season, week, homeTeam.Abbreviation);
        if (eventId == null)
        {
            _logger.LogWarning(
                "No ESPN event ID found for game {GameId} (season {Season}, week {Week}, home {HomeAbbr}). " +
                "Scrape games first to populate event IDs.",
                game.Id, season, week, homeTeam.Abbreviation);
            return 0;
        }

        var response = await FetchJsonAsync<EspnSummaryResponse>($"/summary?event={eventId}");
        if (response?.Boxscore == null)
        {
            _logger.LogWarning("Failed to fetch box score for event {EventId}", eventId);
            return 0;
        }

        int count = 0;
        foreach (var teamStats in response.Boxscore.Players)
        {
            // Find the "passing" category to get passing stats
            var passingCategory = teamStats.Statistics.FirstOrDefault(s =>
                s.Name.Equals("passing", StringComparison.OrdinalIgnoreCase));
            var rushingCategory = teamStats.Statistics.FirstOrDefault(s =>
                s.Name.Equals("rushing", StringComparison.OrdinalIgnoreCase));
            var receivingCategory = teamStats.Statistics.FirstOrDefault(s =>
                s.Name.Equals("receiving", StringComparison.OrdinalIgnoreCase));

            // Build a dictionary of player name -> aggregated stats
            var playerStats = new Dictionary<string, PlayerGameStats>();

            if (passingCategory != null)
                ParseCategory(passingCategory, game.Id, playerStats, ParsePassingStats);
            if (rushingCategory != null)
                ParseCategory(rushingCategory, game.Id, playerStats, ParseRushingStats);
            if (receivingCategory != null)
                ParseCategory(receivingCategory, game.Id, playerStats, ParseReceivingStats);

            foreach (var (playerName, stats) in playerStats)
            {
                var player = await _playerRepository.GetByNameAsync(playerName);
                if (player == null)
                {
                    _logger.LogDebug("Player not found in database: {PlayerName}. Skipping.", playerName);
                    continue;
                }

                stats.PlayerId = player.Id;
                await _statsRepository.UpsertAsync(stats);
                count++;
            }
        }

        return count;
    }

    private static void ParseCategory(
        EspnStatCategory category,
        int gameId,
        Dictionary<string, PlayerGameStats> playerStats,
        Action<PlayerGameStats, List<string>, List<string>> parser)
    {
        foreach (var athlete in category.Athletes)
        {
            var name = athlete.Athlete.DisplayName;
            if (string.IsNullOrEmpty(name)) continue;

            if (!playerStats.TryGetValue(name, out var stats))
            {
                stats = new PlayerGameStats { GameId = gameId };
                playerStats[name] = stats;
            }

            parser(stats, category.Keys, athlete.Stats);
        }
    }

    private static void ParsePassingStats(PlayerGameStats stats, List<string> keys, List<string> values)
    {
        for (int i = 0; i < keys.Count && i < values.Count; i++)
        {
            var value = values[i];
            switch (keys[i].ToUpperInvariant())
            {
                case "C/ATT":
                    // Format is "completions/attempts"
                    var parts = value.Split('/');
                    if (parts.Length == 2)
                    {
                        if (int.TryParse(parts[0], out var cmp)) stats.PassCompletions = cmp;
                        if (int.TryParse(parts[1], out var att)) stats.PassAttempts = att;
                    }
                    break;
                case "YDS":
                    if (int.TryParse(value, out var yds)) stats.PassYards = yds;
                    break;
                case "TD":
                    if (int.TryParse(value, out var td)) stats.PassTouchdowns = td;
                    break;
                case "INT":
                    if (int.TryParse(value, out var ints)) stats.Interceptions = ints;
                    break;
            }
        }
    }

    private static void ParseRushingStats(PlayerGameStats stats, List<string> keys, List<string> values)
    {
        for (int i = 0; i < keys.Count && i < values.Count; i++)
        {
            var value = values[i];
            switch (keys[i].ToUpperInvariant())
            {
                case "CAR":
                    if (int.TryParse(value, out var att)) stats.RushAttempts = att;
                    break;
                case "YDS":
                    if (int.TryParse(value, out var yds)) stats.RushYards = yds;
                    break;
                case "TD":
                    if (int.TryParse(value, out var td)) stats.RushTouchdowns = td;
                    break;
            }
        }
    }

    private static void ParseReceivingStats(PlayerGameStats stats, List<string> keys, List<string> values)
    {
        for (int i = 0; i < keys.Count && i < values.Count; i++)
        {
            var value = values[i];
            switch (keys[i].ToUpperInvariant())
            {
                case "REC":
                    if (int.TryParse(value, out var rec)) stats.Receptions = rec;
                    break;
                case "YDS":
                    if (int.TryParse(value, out var yds)) stats.ReceivingYards = yds;
                    break;
                case "TD":
                    if (int.TryParse(value, out var td)) stats.ReceivingTouchdowns = td;
                    break;
            }
        }
    }
}
