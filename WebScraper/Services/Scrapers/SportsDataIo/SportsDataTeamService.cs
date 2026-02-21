using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.SportsDataIo;

public class SportsDataTeamService : BaseApiService, ITeamScraperService
{
    private readonly ITeamRepository _teamRepository;

    public SportsDataTeamService(
        HttpClient httpClient,
        ILogger<SportsDataTeamService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        ITeamRepository teamRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _teamRepository = teamRepository;
    }

    public async Task<ScrapeResult> ScrapeTeamsAsync()
    {
        _logger.LogInformation("Starting teams scrape from SportsData.io API");

        var teams = await FetchJsonAsync<List<SportsDataTeamDto>>("/scores/json/Teams");
        if (teams == null)
        {
            _logger.LogWarning("Failed to fetch teams from SportsData.io API");
            return ScrapeResult.Failed("Failed to fetch teams from SportsData.io API");
        }

        int count = 0;
        foreach (var dto in teams)
        {
            var team = MapToTeam(dto);
            if (team != null)
            {
                await _teamRepository.UpsertAsync(team);
                count++;
                _logger.LogDebug("Upserted team: {TeamName} ({Abbreviation})", team.Name, team.Abbreviation);
            }
        }

        _logger.LogInformation("SportsData.io teams scrape complete. {Count} teams processed", count);
        return ScrapeResult.Succeeded(count, $"{count} teams processed from SportsData.io API");
    }

    public async Task<ScrapeResult> ScrapeTeamAsync(string abbreviation)
    {
        _logger.LogInformation("Starting single team scrape for {Abbreviation} from SportsData.io API", abbreviation);

        var teams = await FetchJsonAsync<List<SportsDataTeamDto>>("/scores/json/Teams");
        if (teams == null)
        {
            _logger.LogWarning("Failed to fetch teams from SportsData.io API");
            return ScrapeResult.Failed("Failed to fetch teams from SportsData.io API");
        }

        var dto = teams.FirstOrDefault(t =>
            t.Key.Equals(abbreviation, StringComparison.OrdinalIgnoreCase));

        if (dto == null)
        {
            _logger.LogWarning("Team with abbreviation {Abbreviation} not found in SportsData.io response", abbreviation);
            return ScrapeResult.Failed($"Team with abbreviation '{abbreviation}' not found in SportsData.io response");
        }

        var team = MapToTeam(dto);
        if (team != null)
        {
            await _teamRepository.UpsertAsync(team);
            _logger.LogInformation("Upserted team: {TeamName} ({Abbreviation})", team.Name, team.Abbreviation);
            return ScrapeResult.Succeeded(1, $"Team {team.Name} ({team.Abbreviation}) scraped from SportsData.io API");
        }

        return ScrapeResult.Failed($"Failed to map SportsData.io team data for '{abbreviation}'");
    }

    private static Team? MapToTeam(SportsDataTeamDto dto)
    {
        if (string.IsNullOrEmpty(dto.FullName) || string.IsNullOrEmpty(dto.Key))
            return null;

        return new Team
        {
            Name = dto.FullName,
            Abbreviation = dto.Key,
            City = dto.City,
            Conference = dto.Conference,
            Division = dto.Division
        };
    }
}
