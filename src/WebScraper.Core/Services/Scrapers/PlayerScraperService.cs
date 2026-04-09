using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers;

public class PlayerScraperService : BaseScraperService, IPlayerScraperService
{
    private readonly IPlayerRepository _playerRepository;
    private readonly ITeamRepository _teamRepository;

    public PlayerScraperService(
        HttpClient httpClient,
        ILogger<PlayerScraperService> logger,
        IOptions<ScraperSettings> settings,
        RateLimiterService rateLimiter,
        IPlayerRepository playerRepository,
        ITeamRepository teamRepository)
        : base(httpClient, logger, settings, rateLimiter)
    {
        _playerRepository = playerRepository;
        _teamRepository = teamRepository;
    }

    public async Task<ScrapeResult> ScrapeAllPlayersAsync()
    {
        _logger.LogInformation("Starting full player roster scrape for all teams");

        var teams = await _teamRepository.GetAllAsync();
        var teamsList = teams.ToList();
        int totalCount = 0;
        var errors = new List<string>();

        foreach (var team in teamsList)
        {
            var result = await ScrapePlayersAsync(team.Id);
            totalCount += result.RecordsProcessed;
            if (!result.Success)
                errors.Add(result.Message);
        }

        _logger.LogInformation("All player rosters scrape complete. {Count} players processed", totalCount);
        return new ScrapeResult
        {
            Success = errors.Count == 0 || totalCount > 0,
            RecordsProcessed = totalCount,
            Message = $"{totalCount} players processed across {teamsList.Count} teams",
            Errors = errors
        };
    }

    public async Task<ScrapeResult> ScrapePlayersAsync(int teamId)
    {
        var team = await _teamRepository.GetByIdAsync(teamId);
        if (team == null)
        {
            _logger.LogWarning("Team with ID {TeamId} not found", teamId);
            return ScrapeResult.Failed($"Team with ID {teamId} not found");
        }

        _logger.LogInformation("Scraping roster for {TeamName} ({Abbreviation})", team.Name, team.Abbreviation);

        // Pro Football Reference roster URL pattern
        var pfrAbbr = GetPfrAbbreviation(team.Abbreviation);
        var url = $"https://www.pro-football-reference.com/teams/{pfrAbbr}/2025_roster.htm";

        var doc = await FetchPageAsync(url);
        if (doc == null)
        {
            _logger.LogWarning("Failed to fetch roster page for {TeamName}", team.Name);
            return ScrapeResult.Failed($"Failed to fetch roster page for {team.Name}");
        }

        var playerNodes = doc.DocumentNode.SelectNodes("//table[@id='roster']//tbody//tr[not(contains(@class,'thead'))]");
        if (playerNodes == null)
        {
            _logger.LogWarning("No player rows found for {TeamName}", team.Name);
            return ScrapeResult.Failed($"No player rows found for {team.Name}");
        }

        int count = 0;
        foreach (var node in playerNodes)
        {
            var player = ParsePlayerNode(node, team.Id);
            if (player != null)
            {
                await _playerRepository.UpsertAsync(player);
                count++;
                _logger.LogDebug("Upserted player: {PlayerName} ({Position})", player.Name, player.Position);
            }
        }

        _logger.LogInformation("Roster scrape complete for {TeamName}. {Count} players processed", team.Name, count);
        return ScrapeResult.Succeeded(count, $"{count} players processed for {team.Name}");
    }

    private Player? ParsePlayerNode(HtmlNode node, int teamId)
    {
        try
        {
            var nameCell = node.SelectSingleNode(".//td[@data-stat='player']");
            if (nameCell == null) return null;

            var name = HtmlEntity.DeEntitize(nameCell.InnerText).Trim();
            if (string.IsNullOrEmpty(name)) return null;

            var positionCell = node.SelectSingleNode(".//td[@data-stat='pos']");
            var jerseyCell = node.SelectSingleNode(".//th[@data-stat='jersey_number'] | .//td[@data-stat='jersey_number']");
            var heightCell = node.SelectSingleNode(".//td[@data-stat='height']");
            var weightCell = node.SelectSingleNode(".//td[@data-stat='weight']");
            var collegeCell = node.SelectSingleNode(".//td[@data-stat='college']");

            var position = positionCell != null ? HtmlEntity.DeEntitize(positionCell.InnerText).Trim() : "";
            var jerseyText = jerseyCell != null ? HtmlEntity.DeEntitize(jerseyCell.InnerText).Trim() : "";
            var height = heightCell != null ? HtmlEntity.DeEntitize(heightCell.InnerText).Trim() : null;
            var weightText = weightCell != null ? HtmlEntity.DeEntitize(weightCell.InnerText).Trim() : "";
            var college = collegeCell != null ? HtmlEntity.DeEntitize(collegeCell.InnerText).Trim() : null;

            int? jerseyNumber = int.TryParse(jerseyText, out var jn) ? jn : null;
            int? weight = int.TryParse(weightText, out var w) ? w : null;

            return new Player
            {
                Name = name,
                TeamId = teamId,
                Position = position,
                JerseyNumber = jerseyNumber,
                Height = height,
                Weight = weight,
                College = string.IsNullOrEmpty(college) ? null : college
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse player node");
            return null;
        }
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
