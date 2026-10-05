using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Services.Coverage;

namespace WebScraper.Services.Scrapers.ApiSports;

public class ApiSportsTeamService : ApiSportsServiceBase, ITeamScraperService
{
    private readonly ITeamRepository _teamRepository;

    public ApiSportsTeamService(
        HttpClient httpClient,
        ILogger<ApiSportsTeamService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        ITeamRepository teamRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _teamRepository = teamRepository;
    }

    public async Task<ScrapeResult> ScrapeTeamsAsync()
    {
        var season = NflSeasonSchedule.GetCurrentSeason(DateTime.UtcNow);
        _logger.LogInformation("Starting teams scrape for season {Season} from api-sports", season);

        var (teams, error) = await FetchEnvelopeAsync<ApiSportsTeamListItem>($"teams?league=1&season={season}");
        if (teams == null)
            return ScrapeResult.Failed($"api-sports error: {error}");

        var fetchedAt = DateTime.UtcNow;
        var count = 0;
        foreach (var item in teams)
        {
            var team = MapToTeam(item, fetchedAt);
            if (team == null)
                continue;

            await _teamRepository.UpsertAsync(team);
            count++;
            _logger.LogDebug("Upserted team: {TeamName} ({Abbreviation})", team.Name, team.Abbreviation);
        }

        _logger.LogInformation("api-sports teams scrape complete. {Count} teams processed", count);
        return ScrapeResult.Succeeded(count, $"{count} teams processed from api-sports for season {season}");
    }

    public async Task<ScrapeResult> ScrapeTeamAsync(string abbreviation)
    {
        var season = NflSeasonSchedule.GetCurrentSeason(DateTime.UtcNow);
        _logger.LogInformation(
            "Starting single team scrape for {Abbreviation} (season {Season}) from api-sports",
            abbreviation, season);

        var (teams, error) = await FetchEnvelopeAsync<ApiSportsTeamListItem>($"teams?league=1&season={season}");
        if (teams == null)
            return ScrapeResult.Failed($"api-sports error: {error}");

        var fetchedAt = DateTime.UtcNow;
        foreach (var item in teams)
        {
            var team = MapToTeam(item, fetchedAt);
            if (team == null)
                continue;

            if (!team.Abbreviation.Equals(abbreviation, StringComparison.OrdinalIgnoreCase))
                continue;

            await _teamRepository.UpsertAsync(team);
            return ScrapeResult.Succeeded(1, $"Team {team.Name} ({team.Abbreviation}) scraped from api-sports");
        }

        return ScrapeResult.Failed($"Team with abbreviation '{abbreviation}' not found in api-sports response");
    }

    private Team? MapToTeam(ApiSportsTeamListItem item, DateTime fetchedAt)
    {
        var abbreviation = ApiSportsMappings.TeamIdToAbbreviation(item.Team.Id);
        if (abbreviation == null)
        {
            _logger.LogWarning(
                "Skipping api-sports team id {TeamId} ({Name}): no NFL abbreviation mapping",
                item.Team.Id, item.Team.Name);
            return null;
        }

        var (conference, division) = NflTeams.GetDivision(abbreviation);

        return new Team
        {
            Name = item.Team.Name,
            Abbreviation = abbreviation,
            City = item.Team.City ?? string.Empty,
            Conference = conference,
            Division = division,
            DataSource = "ApiSports",
            DataSourceRecordId = item.Team.Id.ToString(),
            DataSourceFetchedAt = fetchedAt
        };
    }
}
