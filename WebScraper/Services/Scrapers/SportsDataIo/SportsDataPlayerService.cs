using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.SportsDataIo;

public class SportsDataPlayerService : BaseApiService, IPlayerScraperService
{
    private readonly IPlayerRepository _playerRepository;
    private readonly ITeamRepository _teamRepository;

    public SportsDataPlayerService(
        HttpClient httpClient,
        ILogger<SportsDataPlayerService> logger,
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
        _logger.LogInformation("Starting full player roster scrape from SportsData.io API for all teams");

        var teams = await _teamRepository.GetAllAsync();
        foreach (var team in teams)
        {
            await ScrapePlayersAsync(team.Id);
        }

        _logger.LogInformation("All player rosters scrape complete via SportsData.io API");
    }

    public async Task ScrapePlayersAsync(int teamId)
    {
        var team = await _teamRepository.GetByIdAsync(teamId);
        if (team == null)
        {
            _logger.LogWarning("Team with ID {TeamId} not found", teamId);
            return;
        }

        _logger.LogInformation("Scraping roster for {TeamName} ({Abbreviation}) from SportsData.io API",
            team.Name, team.Abbreviation);

        var players = await FetchJsonAsync<List<SportsDataPlayerDto>>($"/scores/json/Players/{team.Abbreviation}");
        if (players == null)
        {
            _logger.LogWarning("Failed to fetch players for {TeamName} from SportsData.io API", team.Name);
            return;
        }

        int count = 0;
        foreach (var dto in players)
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
    }

    private static Player? MapToPlayer(SportsDataPlayerDto dto, int teamId)
    {
        if (string.IsNullOrEmpty(dto.Name))
            return null;

        return new Player
        {
            Name = dto.Name,
            TeamId = teamId,
            Position = dto.Position,
            JerseyNumber = dto.Number,
            Height = dto.Height,
            Weight = dto.Weight,
            College = string.IsNullOrEmpty(dto.College) ? null : dto.College
        };
    }
}
