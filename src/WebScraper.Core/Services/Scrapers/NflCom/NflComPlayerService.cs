using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.NflCom;

public class NflComPlayerService : BaseApiService, IPlayerScraperService
{
    private readonly IPlayerRepository _playerRepository;
    private readonly ITeamRepository _teamRepository;

    public NflComPlayerService(
        HttpClient httpClient,
        ILogger<NflComPlayerService> logger,
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
        _logger.LogInformation("Starting full player roster scrape from NFL.com API for all teams");

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

        _logger.LogInformation("All player rosters scrape complete via NFL.com API. {Count} players processed", totalCount);
        return new ScrapeResult
        {
            Success = errors.Count == 0 || totalCount > 0,
            RecordsProcessed = totalCount,
            Message = $"{totalCount} players processed across {teamsList.Count} teams from NFL.com API",
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

        _logger.LogInformation("Scraping roster for {TeamName} ({Abbreviation}) from NFL.com API",
            team.Name, team.Abbreviation);

        var response = await FetchJsonAsync<NflComRosterResponse>($"/teams/{team.Abbreviation}/roster");
        if (response == null)
        {
            _logger.LogWarning("Failed to fetch roster for {TeamName} from NFL.com API", team.Name);
            return ScrapeResult.Failed($"Failed to fetch roster for {team.Name} from NFL.com API");
        }

        int count = 0;
        foreach (var dto in response.Roster)
        {
            var player = MapToPlayer(dto, team.Id);
            if (player != null)
            {
                await _playerRepository.UpsertAsync(player);
                count++;
                _logger.LogDebug("Upserted player: {PlayerName} ({Position})", player.Name, player.Position);
            }
        }

        _logger.LogInformation("Roster scrape complete for {TeamName}. {Count} players processed", team.Name, count);
        return ScrapeResult.Succeeded(count, $"{count} players processed for {team.Name} from NFL.com API");
    }

    private static Player? MapToPlayer(NflComPlayer dto, int teamId)
    {
        if (string.IsNullOrEmpty(dto.DisplayName))
            return null;

        int? jerseyNumber = int.TryParse(dto.JerseyNumber, out var jn) ? jn : null;
        int? weight = int.TryParse(dto.Weight, out var w) ? w : null;

        return new Player
        {
            Name = dto.DisplayName,
            TeamId = teamId,
            Position = dto.Position,
            JerseyNumber = jerseyNumber,
            Height = dto.Height,
            Weight = weight,
            College = string.IsNullOrEmpty(dto.College) ? null : dto.College
        };
    }
}
