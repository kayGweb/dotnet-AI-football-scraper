using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers;

public class GameScraperService : BaseScraperService, IGameScraperService
{
    private readonly IGameRepository _gameRepository;
    private readonly ITeamRepository _teamRepository;

    public GameScraperService(
        HttpClient httpClient,
        ILogger<GameScraperService> logger,
        IOptions<ScraperSettings> settings,
        RateLimiterService rateLimiter,
        IGameRepository gameRepository,
        ITeamRepository teamRepository)
        : base(httpClient, logger, settings, rateLimiter)
    {
        _gameRepository = gameRepository;
        _teamRepository = teamRepository;
    }

    public async Task<ScrapeResult> ScrapeGamesAsync(int season)
    {
        _logger.LogInformation("Starting games scrape for season {Season}", season);

        var url = $"https://www.pro-football-reference.com/years/{season}/games.htm";
        var doc = await FetchPageAsync(url);
        if (doc == null)
        {
            _logger.LogWarning("Failed to fetch games page for season {Season}", season);
            return ScrapeResult.Failed($"Failed to fetch games page for season {season}");
        }

        var gameNodes = doc.DocumentNode.SelectNodes("//table[@id='games']//tbody//tr[not(contains(@class,'thead'))]");
        if (gameNodes == null)
        {
            _logger.LogWarning("No game rows found for season {Season}", season);
            return ScrapeResult.Failed($"No game rows found for season {season}");
        }

        int count = 0;
        foreach (var node in gameNodes)
        {
            var game = await ParseGameNodeAsync(node, season);
            if (game != null)
            {
                await _gameRepository.UpsertAsync(game);
                count++;
            }
        }

        _logger.LogInformation("Games scrape complete for season {Season}. {Count} games processed", season, count);
        return ScrapeResult.Succeeded(count, $"{count} games processed for season {season}");
    }

    public async Task<ScrapeResult> ScrapeGamesAsync(int season, int week)
    {
        _logger.LogInformation("Starting games scrape for season {Season} week {Week}", season, week);

        // PFR puts all weeks on one page, so we fetch the full season and filter
        var url = $"https://www.pro-football-reference.com/years/{season}/games.htm";
        var doc = await FetchPageAsync(url);
        if (doc == null)
        {
            _logger.LogWarning("Failed to fetch games page for season {Season}", season);
            return ScrapeResult.Failed($"Failed to fetch games page for season {season}");
        }

        var gameNodes = doc.DocumentNode.SelectNodes("//table[@id='games']//tbody//tr[not(contains(@class,'thead'))]");
        if (gameNodes == null)
        {
            _logger.LogWarning("No game rows found for season {Season}", season);
            return ScrapeResult.Failed($"No game rows found for season {season}");
        }

        int count = 0;
        foreach (var node in gameNodes)
        {
            var weekCell = node.SelectSingleNode(".//th[@data-stat='week_num']");
            if (weekCell == null) continue;

            var weekText = HtmlEntity.DeEntitize(weekCell.InnerText).Trim();
            if (!int.TryParse(weekText, out var rowWeek) || rowWeek != week) continue;

            var game = await ParseGameNodeAsync(node, season);
            if (game != null)
            {
                await _gameRepository.UpsertAsync(game);
                count++;
            }
        }

        _logger.LogInformation("Games scrape complete for season {Season} week {Week}. {Count} games processed", season, week, count);
        return ScrapeResult.Succeeded(count, $"{count} games processed for season {season} week {week}");
    }

    private async Task<Game?> ParseGameNodeAsync(HtmlNode node, int season)
    {
        try
        {
            var weekCell = node.SelectSingleNode(".//th[@data-stat='week_num']");
            var dateCell = node.SelectSingleNode(".//td[@data-stat='game_date']");
            var winnerCell = node.SelectSingleNode(".//td[@data-stat='winner']");
            var loserCell = node.SelectSingleNode(".//td[@data-stat='loser']");
            var ptsWinCell = node.SelectSingleNode(".//td[@data-stat='pts_win']");
            var ptsLoseCell = node.SelectSingleNode(".//td[@data-stat='pts_lose']");
            var locationCell = node.SelectSingleNode(".//td[@data-stat='game_location']");

            if (weekCell == null || winnerCell == null || loserCell == null) return null;

            var weekText = HtmlEntity.DeEntitize(weekCell.InnerText).Trim();
            if (!int.TryParse(weekText, out var week)) return null;

            // Parse date
            var dateText = dateCell != null ? HtmlEntity.DeEntitize(dateCell.InnerText).Trim() : "";
            DateTime gameDate = DateTime.MinValue;
            if (!string.IsNullOrEmpty(dateText))
            {
                DateTime.TryParse(dateText, out gameDate);
            }

            // Resolve team names to abbreviations
            var winnerName = ExtractTeamAbbreviation(winnerCell);
            var loserName = ExtractTeamAbbreviation(loserCell);
            if (winnerName == null || loserName == null) return null;

            var winnerTeam = await _teamRepository.GetByAbbreviationAsync(winnerName);
            var loserTeam = await _teamRepository.GetByAbbreviationAsync(loserName);
            if (winnerTeam == null || loserTeam == null)
            {
                _logger.LogDebug("Could not find teams: winner={Winner}, loser={Loser}", winnerName, loserName);
                return null;
            }

            // Parse scores
            int? winnerScore = null, loserScore = null;
            if (ptsWinCell != null && int.TryParse(HtmlEntity.DeEntitize(ptsWinCell.InnerText).Trim(), out var ws))
                winnerScore = ws;
            if (ptsLoseCell != null && int.TryParse(HtmlEntity.DeEntitize(ptsLoseCell.InnerText).Trim(), out var ls))
                loserScore = ls;

            // Determine home/away: "@" in location means winner was away
            var location = locationCell != null ? HtmlEntity.DeEntitize(locationCell.InnerText).Trim() : "";
            bool winnerIsAway = location == "@";

            int homeTeamId, awayTeamId;
            int? homeScore, awayScore;

            if (winnerIsAway)
            {
                homeTeamId = loserTeam.Id;
                awayTeamId = winnerTeam.Id;
                homeScore = loserScore;
                awayScore = winnerScore;
            }
            else
            {
                homeTeamId = winnerTeam.Id;
                awayTeamId = loserTeam.Id;
                homeScore = winnerScore;
                awayScore = loserScore;
            }

            return new Game
            {
                Season = season,
                Week = week,
                GameDate = gameDate,
                HomeTeamId = homeTeamId,
                AwayTeamId = awayTeamId,
                HomeScore = homeScore,
                AwayScore = awayScore
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse game node");
            return null;
        }
    }

    private string? ExtractTeamAbbreviation(HtmlNode cell)
    {
        var link = cell.SelectSingleNode(".//a");
        if (link == null) return null;

        var href = link.GetAttributeValue("href", "");
        // Href like /teams/kan/2025.htm -> extract "kan"
        var segments = href.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2) return null;

        var pfrAbbr = segments[1].ToLowerInvariant();
        return PfrToNflAbbreviation(pfrAbbr);
    }

    // Exposed for use by other scrapers that need the same mapping
    internal static string PfrToNflAbbreviation(string pfrAbbr)
    {
        return pfrAbbr switch
        {
            "crd" => "ARI",
            "rav" => "BAL",
            "gnb" => "GB",
            "htx" => "HOU",
            "clt" => "IND",
            "kan" => "KC",
            "sdg" => "LAC",
            "ram" => "LAR",
            "rai" => "LV",
            "nwe" => "NE",
            "nor" => "NO",
            "sfo" => "SF",
            "tam" => "TB",
            "oti" => "TEN",
            _ => pfrAbbr.ToUpperInvariant()
        };
    }
}
