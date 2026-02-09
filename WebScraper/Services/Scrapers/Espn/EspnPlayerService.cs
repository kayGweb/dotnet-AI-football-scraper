using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.Espn;

public class EspnPlayerService : BaseApiService, IPlayerScraperService
{
    private readonly IPlayerRepository _playerRepository;
    private readonly ITeamRepository _teamRepository;

    public EspnPlayerService(
        HttpClient httpClient,
        ILogger<EspnPlayerService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        IPlayerRepository playerRepository,
        ITeamRepository teamRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _playerRepository = playerRepository;
        _teamRepository = teamRepository;
    }

    public async Task ScrapeAllPlayersAsync()
    {
        _logger.LogInformation("Starting full player roster scrape from ESPN API for all teams");

        var teams = await _teamRepository.GetAllAsync();
        foreach (var team in teams)
        {
            await ScrapePlayersAsync(team.Id);
        }

        _logger.LogInformation("All player rosters scrape complete via ESPN API");
    }

    public async Task ScrapePlayersAsync(int teamId)
    {
        var team = await _teamRepository.GetByIdAsync(teamId);
        if (team == null)
        {
            _logger.LogWarning("Team with ID {TeamId} not found", teamId);
            return;
        }

        var espnId = EspnMappings.ToEspnId(team.Abbreviation);
        if (espnId == null)
        {
            _logger.LogWarning("No ESPN ID mapping for team {Abbreviation}", team.Abbreviation);
            return;
        }

        _logger.LogInformation("Scraping roster for {TeamName} ({Abbreviation}) from ESPN API", team.Name, team.Abbreviation);

        var response = await FetchJsonAsync<EspnRosterResponse>($"/teams/{espnId}/roster");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch roster for {TeamName} from ESPN API", team.Name);
            return;
        }

        int count = 0;
        foreach (var category in response.Athletes)
        {
            foreach (var athlete in category.Items)
            {
                var player = MapToPlayer(athlete, team.Id);
                if (player != null)
                {
                    await _playerRepository.UpsertAsync(player);
                    count++;
                    _logger.LogDebug("Upserted player: {PlayerName} ({Position})", player.Name, player.Position);
                }
            }
        }

        _logger.LogInformation("Roster scrape complete for {TeamName}. {Count} players processed", team.Name, count);
    }

    private static Player? MapToPlayer(EspnAthlete athlete, int teamId)
    {
        if (string.IsNullOrEmpty(athlete.DisplayName))
            return null;

        int? jerseyNumber = int.TryParse(athlete.Jersey, out var jn) ? jn : null;

        // ESPN height is in inches — convert to "X-Y" format
        string? height = null;
        if (athlete.Height.HasValue)
        {
            var totalInches = (int)athlete.Height.Value;
            var feet = totalInches / 12;
            var inches = totalInches % 12;
            height = $"{feet}-{inches}";
        }

        int? weight = athlete.Weight.HasValue ? (int)athlete.Weight.Value : null;

        return new Player
        {
            Name = athlete.DisplayName,
            TeamId = teamId,
            Position = athlete.Position?.Abbreviation ?? "",
            JerseyNumber = jerseyNumber,
            Height = height,
            Weight = weight,
            College = athlete.College?.Name
        };
    }
}
