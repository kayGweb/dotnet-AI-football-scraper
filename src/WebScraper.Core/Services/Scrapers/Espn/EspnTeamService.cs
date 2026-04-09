using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.Espn;

public class EspnTeamService : BaseApiService, ITeamScraperService
{
    private readonly ITeamRepository _teamRepository;

    public EspnTeamService(
        HttpClient httpClient,
        ILogger<EspnTeamService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        ITeamRepository teamRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _teamRepository = teamRepository;
    }

    public async Task<ScrapeResult> ScrapeTeamsAsync()
    {
        _logger.LogInformation("Starting teams scrape from ESPN API");

        var response = await FetchJsonAsync<EspnTeamsResponse>("/teams");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch teams from ESPN API");
            return ScrapeResult.Failed("Failed to fetch teams from ESPN API");
        }

        var espnTeams = response.Sports
            .SelectMany(s => s.Leagues)
            .SelectMany(l => l.Teams)
            .Select(tw => tw.Team)
            .ToList();

        int count = 0;
        foreach (var espnTeam in espnTeams)
        {
            var team = MapToTeam(espnTeam);
            if (team != null)
            {
                await _teamRepository.UpsertAsync(team);
                count++;
                _logger.LogDebug("Upserted team: {TeamName} ({Abbreviation})", team.Name, team.Abbreviation);
            }
        }

        _logger.LogInformation("ESPN teams scrape complete. {Count} teams processed", count);
        return ScrapeResult.Succeeded(count, $"{count} teams processed from ESPN API");
    }

    public async Task<ScrapeResult> ScrapeTeamAsync(string abbreviation)
    {
        _logger.LogInformation("Starting single team scrape for {Abbreviation} from ESPN API", abbreviation);

        var response = await FetchJsonAsync<EspnTeamsResponse>("/teams");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch teams from ESPN API");
            return ScrapeResult.Failed("Failed to fetch teams from ESPN API");
        }

        var espnTeams = response.Sports
            .SelectMany(s => s.Leagues)
            .SelectMany(l => l.Teams)
            .Select(tw => tw.Team)
            .ToList();

        foreach (var espnTeam in espnTeams)
        {
            var nflAbbr = EspnMappings.ToNflAbbreviation(espnTeam.Id, espnTeam.Abbreviation);
            if (nflAbbr.Equals(abbreviation, StringComparison.OrdinalIgnoreCase))
            {
                var team = MapToTeam(espnTeam);
                if (team != null)
                {
                    await _teamRepository.UpsertAsync(team);
                    _logger.LogInformation("Upserted team: {TeamName} ({Abbreviation})", team.Name, team.Abbreviation);
                    return ScrapeResult.Succeeded(1, $"Team {team.Name} ({team.Abbreviation}) scraped from ESPN API");
                }
                return ScrapeResult.Failed($"Failed to map ESPN team data for '{abbreviation}'");
            }
        }

        _logger.LogWarning("Team with abbreviation {Abbreviation} not found in ESPN response", abbreviation);
        return ScrapeResult.Failed($"Team with abbreviation '{abbreviation}' not found in ESPN response");
    }

    private static Team? MapToTeam(EspnTeam espnTeam)
    {
        var nflAbbr = EspnMappings.ToNflAbbreviation(espnTeam.Id, espnTeam.Abbreviation);
        var (conference, division) = EspnMappings.GetDivision(nflAbbr);

        if (string.IsNullOrEmpty(espnTeam.DisplayName))
            return null;

        return new Team
        {
            Name = espnTeam.DisplayName,
            Abbreviation = nflAbbr,
            City = espnTeam.Location,
            Conference = conference,
            Division = division
        };
    }
}
