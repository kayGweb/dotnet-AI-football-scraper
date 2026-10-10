using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;
using WebScraper.Services.Coverage;

namespace WebScraper.Services.Scrapers.ApiSports;

/// <summary>
/// Rosters from <c>GET /players?team={apiSportsTeamId}&amp;season={season}</c> (one call per team).
/// Players are keyed on <c>(DataSource = "ApiSports", DataSourceRecordId = api-sports player id)</c>
/// and linked to the team-season via <see cref="PlayerTeamSeason"/>.
/// </summary>
public class ApiSportsPlayerService : ApiSportsServiceBase, IPlayerScraperService
{
    public const string DataSourceName = "ApiSports";

    private readonly IPlayerRepository _playerRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ITeamSeasonRepository _teamSeasonRepository;
    private readonly IPlayerTeamSeasonRepository _playerTeamSeasonRepository;

    public ApiSportsPlayerService(
        HttpClient httpClient,
        ILogger<ApiSportsPlayerService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        IPlayerRepository playerRepository,
        ITeamRepository teamRepository,
        ITeamSeasonRepository teamSeasonRepository,
        IPlayerTeamSeasonRepository playerTeamSeasonRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _playerRepository = playerRepository;
        _teamRepository = teamRepository;
        _teamSeasonRepository = teamSeasonRepository;
        _playerTeamSeasonRepository = playerTeamSeasonRepository;
    }

    public async Task<ScrapeResult> ScrapeAllPlayersAsync()
    {
        var season = NflSeasonSchedule.GetCurrentSeason(DateTime.UtcNow);
        _logger.LogInformation("Starting full roster scrape for season {Season} from api-sports", season);

        var teams = (await _teamRepository.GetAllAsync()).ToList();
        if (teams.Count == 0)
            return ScrapeResult.Failed("No teams in database. Scrape teams first.");

        var total = 0;
        var errors = new List<string>();

        foreach (var team in teams)
        {
            var result = await ScrapeTeamRosterAsync(team, season);
            total += result.RecordsProcessed;
            if (!result.Success)
                errors.Add(result.Message);
        }

        _logger.LogInformation(
            "api-sports roster scrape complete for season {Season}. {Count} players processed across {Teams} teams ({Errors} errors)",
            season, total, teams.Count, errors.Count);

        return new ScrapeResult
        {
            Success = errors.Count == 0 || total > 0,
            RecordsProcessed = total,
            RecordsFailed = errors.Count,
            Message = $"{total} players processed across {teams.Count} teams from api-sports for season {season}",
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

        var season = NflSeasonSchedule.GetCurrentSeason(DateTime.UtcNow);
        return await ScrapeTeamRosterAsync(team, season);
    }

    private async Task<ScrapeResult> ScrapeTeamRosterAsync(Team team, int season)
    {
        var apiTeamId = ApiSportsMappings.AbbreviationToTeamId(team.Abbreviation);
        if (apiTeamId == null)
        {
            _logger.LogWarning("No api-sports team id mapping for {Abbreviation}", team.Abbreviation);
            return ScrapeResult.Failed($"No api-sports team id mapping for team {team.Abbreviation}");
        }

        _logger.LogInformation(
            "Scraping roster for {TeamName} ({Abbreviation}, api-sports id {ApiTeamId}) season {Season}",
            team.Name, team.Abbreviation, apiTeamId, season);

        var (players, error) = await FetchEnvelopeAsync<ApiSportsPlayer>($"players?team={apiTeamId}&season={season}");
        if (players == null)
            return ScrapeResult.Failed($"api-sports error for {team.Abbreviation} roster: {error}");

        var teamSeason = await _teamSeasonRepository.EnsureFromTeamAsync(team, season);
        var fetchedAt = DateTime.UtcNow;
        var count = 0;

        foreach (var item in players)
        {
            var player = MapToPlayer(item, team.Id, fetchedAt);
            if (player == null)
                continue;

            var saved = await _playerRepository.UpsertByExternalIdAsync(player);
            await _playerTeamSeasonRepository.UpsertAsync(saved.Id, teamSeason.Id, season);
            count++;
            _logger.LogDebug("Upserted player: {PlayerName} ({Position}) [{RecordId}]", saved.Name, saved.Position, saved.DataSourceRecordId);
        }

        _logger.LogInformation("Roster scrape complete for {TeamName}. {Count} players processed", team.Name, count);
        return ScrapeResult.Succeeded(count, $"{count} players processed for {team.Name} from api-sports (season {season})");
    }

    public static Player? MapToPlayer(ApiSportsPlayer item, int teamId, DateTime fetchedAt)
    {
        if (item.Id <= 0 || string.IsNullOrWhiteSpace(item.Name))
            return null;

        return new Player
        {
            Name = item.Name.Trim(),
            TeamId = teamId,
            Position = item.Position?.Trim() ?? string.Empty,
            JerseyNumber = item.Number > 0 ? item.Number : null,
            Height = ApiSportsMappings.ParseHeight(item.Height),
            Weight = ApiSportsMappings.ParseWeight(item.Weight),
            College = string.IsNullOrWhiteSpace(item.College) ? null : item.College.Trim(),
            DataSource = DataSourceName,
            DataSourceRecordId = item.Id.ToString(),
            DataSourceFetchedAt = fetchedAt
        };
    }
}
