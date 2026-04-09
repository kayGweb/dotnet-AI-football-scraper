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

    public async Task<ScrapeResult> ScrapeTeamsAsync()
    {
        _logger.LogInformation("Starting teams scrape from NFL.com API");

        var response = await FetchJsonAsync<NflComTeamsResponse>("/teams");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch teams from NFL.com API");
            return ScrapeResult.Failed("Failed to fetch teams from NFL.com API");
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
        return ScrapeResult.Succeeded(count, $"{count} teams processed from NFL.com API");
    }

    public async Task<ScrapeResult> ScrapeTeamAsync(string abbreviation)
    {
        _logger.LogInformation("Starting single team scrape for {Abbreviation} from NFL.com API", abbreviation);

        var response = await FetchJsonAsync<NflComTeamsResponse>("/teams");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch teams from NFL.com API");
            return ScrapeResult.Failed("Failed to fetch teams from NFL.com API");
        }

        var dto = response.Teams.FirstOrDefault(t =>
            t.Abbreviation.Equals(abbreviation, StringComparison.OrdinalIgnoreCase));

        if (dto == null)
        {
            _logger.LogWarning("Team with abbreviation {Abbreviation} not found in NFL.com response", abbreviation);
            return ScrapeResult.Failed($"Team with abbreviation '{abbreviation}' not found in NFL.com response");
        }

        var team = MapToTeam(dto);
        if (team != null)
        {
            await _teamRepository.UpsertAsync(team);
            _logger.LogInformation("Upserted team: {TeamName} ({Abbreviation})", team.Name, team.Abbreviation);
            return ScrapeResult.Succeeded(1, $"Team {team.Name} ({team.Abbreviation}) scraped from NFL.com API");
        }

        return ScrapeResult.Failed($"Failed to map NFL.com team data for '{abbreviation}'");
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
