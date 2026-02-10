using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.NflCom;

public class NflComTeamService : BaseApiService, ITeamScraperService
{
    private readonly ITeamRepository _teamRepository;

    public NflComTeamService(
        HttpClient httpClient,
        ILogger<NflComTeamService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        ITeamRepository teamRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _teamRepository = teamRepository;
    }

    public async Task ScrapeTeamsAsync()
    {
        _logger.LogInformation("Starting teams scrape from NFL.com API");

        var response = await FetchJsonAsync<NflComTeamsResponse>("/teams");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch teams from NFL.com API");
            return;
        }

        int count = 0;
        foreach (var dto in response.Teams)
        {
            var team = MapToTeam(dto);
            if (team != null)
            {
                await _teamRepository.UpsertAsync(team);
                count++;
                _logger.LogDebug("Upserted team: {TeamName} ({Abbreviation})", team.Name, team.Abbreviation);
            }
        }

        _logger.LogInformation("NFL.com teams scrape complete. {Count} teams processed", count);
    }

    public async Task ScrapeTeamAsync(string abbreviation)
    {
        _logger.LogInformation("Starting single team scrape for {Abbreviation} from NFL.com API", abbreviation);

        var response = await FetchJsonAsync<NflComTeamsResponse>("/teams");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch teams from NFL.com API");
            return;
        }

        var dto = response.Teams.FirstOrDefault(t =>
            t.Abbreviation.Equals(abbreviation, StringComparison.OrdinalIgnoreCase));

        if (dto == null)
        {
            _logger.LogWarning("Team with abbreviation {Abbreviation} not found in NFL.com response", abbreviation);
            return;
        }

        var team = MapToTeam(dto);
        if (team != null)
        {
            await _teamRepository.UpsertAsync(team);
            _logger.LogInformation("Upserted team: {TeamName} ({Abbreviation})", team.Name, team.Abbreviation);
        }
    }

    private static Team? MapToTeam(NflComTeam dto)
    {
        if (string.IsNullOrEmpty(dto.FullName) || string.IsNullOrEmpty(dto.Abbreviation))
            return null;

        return new Team
        {
            Name = dto.FullName,
            Abbreviation = dto.Abbreviation,
            City = dto.CityStateRegion,
            Conference = dto.Conference,
            Division = dto.Division
        };
    }
}
