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

    public async Task<ScrapeResult> ScrapeAllPlayersAsync()
    {
        _logger.LogInformation("Starting full player roster scrape from MySportsFeeds API for all teams");

        var teams = await _teamRepository.GetAllAsync();
        var teamsList = teams.ToList();
        int totalCount = 0;
        var errors = new List<string>();

        foreach (var team in teamsList)
        {
            var result = await ScrapePlayersAsync(team.Id);
            totalCount += result.RecordsProcessed;
            if (!result.Success)
                errors.Add(result.Message);
        }

        _logger.LogInformation("All player rosters scrape complete via MySportsFeeds API. {Count} players processed", totalCount);
        return new ScrapeResult
        {
            Success = errors.Count == 0 || totalCount > 0,
            RecordsProcessed = totalCount,
            Message = $"{totalCount} players processed across {teamsList.Count} teams from MySportsFeeds API",
            Errors = errors
        };
    }

    public async Task<ScrapeResult> ScrapePlayersAsync(int teamId)
    {
        var team = await _teamRepository.GetByIdAsync(teamId);
        if (team == null)
        {
            _logger.LogWarning("Team with ID {TeamId} not found", teamId);
            return ScrapeResult.Failed($"Team with ID {teamId} not found");
        }

        _logger.LogInformation("Scraping roster for {TeamName} ({Abbreviation}) from MySportsFeeds API",
            team.Name, team.Abbreviation);

        var season = DateTime.Now.Year;
        var response = await FetchJsonAsync<MySportsFeedsPlayersResponse>(
            $"/players.json?team={team.Abbreviation}&season={season}");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch players for {TeamName} from MySportsFeeds API", team.Name);
            return ScrapeResult.Failed($"Failed to fetch players for {team.Name} from MySportsFeeds API");
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
        return ScrapeResult.Succeeded(count, $"{count} players processed for {team.Name} from MySportsFeeds API");
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
