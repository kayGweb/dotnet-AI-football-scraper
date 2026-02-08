using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers;

public class TeamScraperService : BaseScraperService, ITeamScraperService
{
    private readonly ITeamRepository _teamRepository;

    private static readonly Dictionary<string, (string Conference, string Division)> DivisionLookup = new()
    {
        // AFC
        { "buf", ("AFC", "East") }, { "mia", ("AFC", "East") }, { "nwe", ("AFC", "East") }, { "nyj", ("AFC", "East") },
        { "bal", ("AFC", "North") }, { "cin", ("AFC", "North") }, { "cle", ("AFC", "North") }, { "pit", ("AFC", "North") },
        { "hou", ("AFC", "South") }, { "clt", ("AFC", "South") }, { "jax", ("AFC", "South") }, { "oti", ("AFC", "South") },
        { "den", ("AFC", "West") }, { "kan", ("AFC", "West") }, { "rai", ("AFC", "West") }, { "sdg", ("AFC", "West") },
        // NFC
        { "dal", ("NFC", "East") }, { "nyg", ("NFC", "East") }, { "phi", ("NFC", "East") }, { "was", ("NFC", "East") },
        { "chi", ("NFC", "North") }, { "det", ("NFC", "North") }, { "gnb", ("NFC", "North") }, { "min", ("NFC", "North") },
        { "atl", ("NFC", "South") }, { "car", ("NFC", "South") }, { "nor", ("NFC", "South") }, { "tam", ("NFC", "South") },
        { "crd", ("NFC", "West") }, { "ram", ("NFC", "West") }, { "sfo", ("NFC", "West") }, { "sea", ("NFC", "West") },
    };

    // Maps PFR abbreviation -> standard NFL abbreviation
    private static readonly Dictionary<string, string> AbbreviationMap = new()
    {
        { "crd", "ARI" }, { "atl", "ATL" }, { "rav", "BAL" }, { "bal", "BAL" }, { "buf", "BUF" },
        { "car", "CAR" }, { "chi", "CHI" }, { "cin", "CIN" }, { "cle", "CLE" }, { "dal", "DAL" },
        { "den", "DEN" }, { "det", "DET" }, { "gnb", "GB" },  { "hou", "HOU" }, { "clt", "IND" },
        { "jax", "JAX" }, { "kan", "KC" },  { "rai", "LV" },  { "sdg", "LAC" }, { "ram", "LAR" },
        { "mia", "MIA" }, { "min", "MIN" }, { "nwe", "NE" },  { "nor", "NO" },  { "nyg", "NYG" },
        { "nyj", "NYJ" }, { "phi", "PHI" }, { "pit", "PIT" }, { "sfo", "SF" },  { "sea", "SEA" },
        { "tam", "TB" },  { "oti", "TEN" }, { "was", "WAS" },
    };

    public TeamScraperService(
        HttpClient httpClient,
        ILogger<TeamScraperService> logger,
        IOptions<ScraperSettings> settings,
        RateLimiterService rateLimiter,
        ITeamRepository teamRepository)
        : base(httpClient, logger, settings, rateLimiter)
    {
        _teamRepository = teamRepository;
    }

    public async Task ScrapeTeamsAsync()
    {
        _logger.LogInformation("Starting teams scrape from Pro Football Reference");

        var doc = await FetchPageAsync("https://www.pro-football-reference.com/teams/");
        if (doc == null)
        {
            _logger.LogWarning("Failed to fetch teams page");
            return;
        }

        var teamNodes = doc.DocumentNode.SelectNodes("//table[@id='teams_active']//tbody//tr[not(contains(@class,'thead'))]");
        if (teamNodes == null)
        {
            _logger.LogWarning("No team rows found in teams_active table");
            return;
        }

        int count = 0;
        foreach (var node in teamNodes)
        {
            var team = ParseTeamNode(node);
            if (team != null)
            {
                await _teamRepository.UpsertAsync(team);
                count++;
                _logger.LogDebug("Upserted team: {TeamName} ({Abbreviation})", team.Name, team.Abbreviation);
            }
        }

        _logger.LogInformation("Teams scrape complete. {Count} teams processed", count);
    }

    public async Task ScrapeTeamAsync(string abbreviation)
    {
        _logger.LogInformation("Starting single team scrape for {Abbreviation}", abbreviation);

        var doc = await FetchPageAsync("https://www.pro-football-reference.com/teams/");
        if (doc == null)
        {
            _logger.LogWarning("Failed to fetch teams page");
            return;
        }

        var teamNodes = doc.DocumentNode.SelectNodes("//table[@id='teams_active']//tbody//tr[not(contains(@class,'thead'))]");
        if (teamNodes == null)
        {
            _logger.LogWarning("No team rows found in teams_active table");
            return;
        }

        foreach (var node in teamNodes)
        {
            var team = ParseTeamNode(node);
            if (team != null && team.Abbreviation.Equals(abbreviation, StringComparison.OrdinalIgnoreCase))
            {
                await _teamRepository.UpsertAsync(team);
                _logger.LogInformation("Upserted team: {TeamName} ({Abbreviation})", team.Name, team.Abbreviation);
                return;
            }
        }

        _logger.LogWarning("Team with abbreviation {Abbreviation} not found on page", abbreviation);
    }

    private Team? ParseTeamNode(HtmlNode node)
    {
        try
        {
            // The team name cell typically contains a link like /teams/kan/
            var nameCell = node.SelectSingleNode(".//th[@data-stat='team_name']");
            if (nameCell == null) return null;

            var link = nameCell.SelectSingleNode(".//a");
            if (link == null) return null;

            var teamName = HtmlEntity.DeEntitize(link.InnerText).Trim();
            if (string.IsNullOrEmpty(teamName)) return null;

            var href = link.GetAttributeValue("href", "");
            // Extract PFR abbreviation from href like /teams/kan/
            var pfrAbbr = href.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault()?.ToLowerInvariant();

            if (string.IsNullOrEmpty(pfrAbbr)) return null;

            var abbreviation = AbbreviationMap.GetValueOrDefault(pfrAbbr, pfrAbbr.ToUpperInvariant());
            var (conference, division) = DivisionLookup.GetValueOrDefault(pfrAbbr, ("", ""));

            // Extract city from team name (everything before last word, roughly)
            var city = ExtractCity(teamName);

            return new Team
            {
                Name = teamName,
                Abbreviation = abbreviation,
                City = city,
                Conference = conference,
                Division = division
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse team node");
            return null;
        }
    }

    private static string ExtractCity(string teamName)
    {
        // Handle two-word team names (e.g., "New England Patriots" -> "New England")
        var parts = teamName.Split(' ');
        if (parts.Length <= 1) return teamName;

        // The last word is the mascot — everything else is the city
        return string.Join(' ', parts.Take(parts.Length - 1));
    }
}
