using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers;

public class StatsScraperService : BaseScraperService, IStatsScraperService
{
    private readonly IStatsRepository _statsRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IGameRepository _gameRepository;

    public StatsScraperService(
        HttpClient httpClient,
        ILogger<StatsScraperService> logger,
        IOptions<ScraperSettings> settings,
        RateLimiterService rateLimiter,
        IStatsRepository statsRepository,
        IPlayerRepository playerRepository,
        IGameRepository gameRepository)
        : base(httpClient, logger, settings, rateLimiter)
    {
        _statsRepository = statsRepository;
        _playerRepository = playerRepository;
        _gameRepository = gameRepository;
    }

    public async Task ScrapePlayerStatsAsync(int season, int week)
    {
        _logger.LogInformation("Starting player stats scrape for season {Season} week {Week}", season, week);

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
            var count = await ScrapeBoxScoreAsync(game);
            totalStats += count;
        }

        _logger.LogInformation("Player stats scrape complete for season {Season} week {Week}. {Count} stat lines processed", season, week, totalStats);
    }

    private async Task<int> ScrapeBoxScoreAsync(Game game)
    {
        // PFR box score URL pattern: /boxscores/YYYYMMDD0<home_pfr_abbr>.htm
        var homeTeam = game.HomeTeam;
        if (homeTeam == null)
        {
            _logger.LogWarning("Home team not loaded for game {GameId}", game.Id);
            return 0;
        }

        var pfrAbbr = GetPfrAbbreviation(homeTeam.Abbreviation);
        var dateStr = game.GameDate.ToString("yyyyMMdd");
        var url = $"https://www.pro-football-reference.com/boxscores/{dateStr}0{pfrAbbr}.htm";

        var doc = await FetchPageAsync(url);
        if (doc == null)
        {
            _logger.LogWarning("Failed to fetch box score for game {GameId}", game.Id);
            return 0;
        }

        int count = 0;

        // Scrape passing stats
        count += await ScrapeStatTableAsync(doc, game, "player_offense",
            (node, playerId) => ParseOffensiveStats(node, playerId, game.Id));

        return count;
    }

    private async Task<int> ScrapeStatTableAsync(
        HtmlDocument doc,
        Game game,
        string tableId,
        Func<HtmlNode, int, PlayerGameStats?> parser)
    {
        var rows = doc.DocumentNode.SelectNodes($"//table[@id='{tableId}']//tbody//tr[not(contains(@class,'thead'))]");
        if (rows == null) return 0;

        int count = 0;
        foreach (var row in rows)
        {
            var playerCell = row.SelectSingleNode(".//th[@data-stat='player'] | .//td[@data-stat='player']");
            if (playerCell == null) continue;

            var playerName = HtmlEntity.DeEntitize(playerCell.InnerText).Trim();
            if (string.IsNullOrEmpty(playerName)) continue;

            var player = await _playerRepository.GetByNameAsync(playerName);
            if (player == null)
            {
                _logger.LogDebug("Player not found in database: {PlayerName}. Skipping.", playerName);
                continue;
            }

            var stats = parser(row, player.Id);
            if (stats != null)
            {
                await _statsRepository.UpsertAsync(stats);
                count++;
            }
        }

        return count;
    }

    private PlayerGameStats? ParseOffensiveStats(HtmlNode row, int playerId, int gameId)
    {
        try
        {
            return new PlayerGameStats
            {
                PlayerId = playerId,
                GameId = gameId,
                PassCompletions = ParseIntStat(row, "pass_cmp"),
                PassAttempts = ParseIntStat(row, "pass_att"),
                PassYards = ParseIntStat(row, "pass_yds"),
                PassTouchdowns = ParseIntStat(row, "pass_td"),
                Interceptions = ParseIntStat(row, "pass_int"),
                RushAttempts = ParseIntStat(row, "rush_att"),
                RushYards = ParseIntStat(row, "rush_yds"),
                RushTouchdowns = ParseIntStat(row, "rush_td"),
                Receptions = ParseIntStat(row, "rec"),
                ReceivingYards = ParseIntStat(row, "rec_yds"),
                ReceivingTouchdowns = ParseIntStat(row, "rec_td"),
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse offensive stats for player {PlayerId}", playerId);
            return null;
        }
    }

    private static int ParseIntStat(HtmlNode row, string dataStat)
    {
        var cell = row.SelectSingleNode($".//td[@data-stat='{dataStat}']");
        if (cell == null) return 0;

        var text = HtmlEntity.DeEntitize(cell.InnerText).Trim();
        return int.TryParse(text, out var value) ? value : 0;
    }

    private static string GetPfrAbbreviation(string nflAbbr)
    {
        return nflAbbr.ToUpperInvariant() switch
        {
            "ARI" => "crd",
            "BAL" => "rav",
            "GB" => "gnb",
            "HOU" => "htx",
            "IND" => "clt",
            "KC" => "kan",
            "LAC" => "sdg",
            "LAR" => "ram",
            "LV" => "rai",
            "NE" => "nwe",
            "NO" => "nor",
            "SF" => "sfo",
            "TB" => "tam",
            "TEN" => "oti",
            "WAS" => "was",
            _ => nflAbbr.ToLowerInvariant()
        };
    }
}
