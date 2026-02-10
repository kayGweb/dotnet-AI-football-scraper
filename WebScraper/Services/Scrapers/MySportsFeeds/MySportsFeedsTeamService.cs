using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.MySportsFeeds;

public class MySportsFeedsTeamService : BaseApiService, ITeamScraperService
{
    private readonly ITeamRepository _teamRepository;

    public MySportsFeedsTeamService(
        HttpClient httpClient,
        ILogger<MySportsFeedsTeamService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        ITeamRepository teamRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _teamRepository = teamRepository;
    }

    public async Task ScrapeTeamsAsync()
    {
        _logger.LogInformation("Starting teams scrape from MySportsFeeds API");

        var season = DateTime.Now.Year;
        var response = await FetchJsonAsync<MySportsFeedsTeamsResponse>($"/{season}/teams.json");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch teams from MySportsFeeds API");
            return;
        }

        int count = 0;
        foreach (var wrapper in response.Teams)
        {
            var team = MapToTeam(wrapper.Team);
            if (team != null)
            {
                await _teamRepository.UpsertAsync(team);
                count++;
                _logger.LogDebug("Upserted team: {TeamName} ({Abbreviation})", team.Name, team.Abbreviation);
            }
        }

        _logger.LogInformation("MySportsFeeds teams scrape complete. {Count} teams processed", count);
    }

    public async Task ScrapeTeamAsync(string abbreviation)
    {
        _logger.LogInformation("Starting single team scrape for {Abbreviation} from MySportsFeeds API", abbreviation);

        var season = DateTime.Now.Year;
        var response = await FetchJsonAsync<MySportsFeedsTeamsResponse>($"/{season}/teams.json");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch teams from MySportsFeeds API");
            return;
        }

        var wrapper = response.Teams.FirstOrDefault(t =>
            t.Team.Abbreviation.Equals(abbreviation, StringComparison.OrdinalIgnoreCase));

        if (wrapper == null)
        {
            _logger.LogWarning("Team with abbreviation {Abbreviation} not found in MySportsFeeds response", abbreviation);
            return;
        }

        var team = MapToTeam(wrapper.Team);
        if (team != null)
        {
            await _teamRepository.UpsertAsync(team);
            _logger.LogInformation("Upserted team: {TeamName} ({Abbreviation})", team.Name, team.Abbreviation);
        }
    }

    private static Team? MapToTeam(MySportsFeedsTeam dto)
    {
        if (string.IsNullOrEmpty(dto.Name) || string.IsNullOrEmpty(dto.Abbreviation))
            return null;

        return new Team
        {
            Name = dto.Name,
            Abbreviation = dto.Abbreviation,
            City = dto.City,
            Conference = dto.Conference ?? "",
            Division = dto.Division ?? ""
        };
    }
}
