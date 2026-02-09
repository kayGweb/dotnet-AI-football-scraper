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

    public async Task ScrapeTeamsAsync()
    {
        _logger.LogInformation("Starting teams scrape from ESPN API");

        var response = await FetchJsonAsync<EspnTeamsResponse>("/teams");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch teams from ESPN API");
            return;
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
    }

    public async Task ScrapeTeamAsync(string abbreviation)
    {
        _logger.LogInformation("Starting single team scrape for {Abbreviation} from ESPN API", abbreviation);

        var response = await FetchJsonAsync<EspnTeamsResponse>("/teams");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch teams from ESPN API");
            return;
        }

        var espnTeams = response.Sports
            .SelectMany(s => s.Leagues)
            .SelectMany(l => l.Teams)
            .Select(tw => tw.Team)
            .ToList();

        foreach (var espnTeam in espnTeams)
        {
            var nflAbbr = EspnMappings.ToNflAbbreviation(espnTeam.Id);
            if (nflAbbr.Equals(abbreviation, StringComparison.OrdinalIgnoreCase))
            {
                var team = MapToTeam(espnTeam);
                if (team != null)
                {
                    await _teamRepository.UpsertAsync(team);
                    _logger.LogInformation("Upserted team: {TeamName} ({Abbreviation})", team.Name, team.Abbreviation);
                }
                return;
            }
        }

        _logger.LogWarning("Team with abbreviation {Abbreviation} not found in ESPN response", abbreviation);
    }

    private static Team? MapToTeam(EspnTeam espnTeam)
    {
        var nflAbbr = EspnMappings.ToNflAbbreviation(espnTeam.Id);
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
