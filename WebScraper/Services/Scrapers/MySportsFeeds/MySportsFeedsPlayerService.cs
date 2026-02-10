using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.MySportsFeeds;

public class MySportsFeedsPlayerService : BaseApiService, IPlayerScraperService
{
    private readonly IPlayerRepository _playerRepository;
    private readonly ITeamRepository _teamRepository;

    public MySportsFeedsPlayerService(
        HttpClient httpClient,
        ILogger<MySportsFeedsPlayerService> logger,
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
        _logger.LogInformation("Starting full player roster scrape from MySportsFeeds API for all teams");

        var teams = await _teamRepository.GetAllAsync();
        foreach (var team in teams)
        {
            await ScrapePlayersAsync(team.Id);
        }

        _logger.LogInformation("All player rosters scrape complete via MySportsFeeds API");
    }

    public async Task ScrapePlayersAsync(int teamId)
    {
        var team = await _teamRepository.GetByIdAsync(teamId);
        if (team == null)
        {
            _logger.LogWarning("Team with ID {TeamId} not found", teamId);
            return;
        }

        _logger.LogInformation("Scraping roster for {TeamName} ({Abbreviation}) from MySportsFeeds API",
            team.Name, team.Abbreviation);

        var season = DateTime.Now.Year;
        var response = await FetchJsonAsync<MySportsFeedsPlayersResponse>(
            $"/players.json?team={team.Abbreviation}&season={season}");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch players for {TeamName} from MySportsFeeds API", team.Name);
            return;
        }

        int count = 0;
        foreach (var wrapper in response.Players)
        {
            var player = MapToPlayer(wrapper.Player, team.Id);
            if (player != null)
            {
                await _playerRepository.UpsertAsync(player);
                count++;
                _logger.LogDebug("Upserted player: {PlayerName} ({Position})", player.Name, player.Position);
            }
        }

        _logger.LogInformation("Roster scrape complete for {TeamName}. {Count} players processed", team.Name, count);
    }

    private static Player? MapToPlayer(MySportsFeedsPlayer dto, int teamId)
    {
        var fullName = $"{dto.FirstName} {dto.LastName}".Trim();
        if (string.IsNullOrEmpty(fullName))
            return null;

        return new Player
        {
            Name = fullName,
            TeamId = teamId,
            Position = dto.Position ?? "",
            JerseyNumber = dto.JerseyNumber,
            Height = dto.Height,
            Weight = dto.Weight,
            College = string.IsNullOrEmpty(dto.College) ? null : dto.College
        };
    }
}
