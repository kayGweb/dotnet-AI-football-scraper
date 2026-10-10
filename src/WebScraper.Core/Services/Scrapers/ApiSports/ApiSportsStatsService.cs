using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.ApiSports;

/// <summary>
/// Box scores for one week: for every api-sports game already in <c>Games</c>
/// (<c>DataSource = "ApiSports"</c>), calls <c>/games/statistics/players?id=</c> for
/// <see cref="PlayerGameStats"/> and <c>/games/statistics/teams?id=</c> for
/// <see cref="TeamGameStats"/>. Players that are not in the database yet are created from the
/// box score (keyed on the api-sports player id) so stats never wait on a roster scrape.
/// </summary>
public class ApiSportsStatsService : ApiSportsServiceBase, IStatsScraperService
{
    public const string DataSourceName = "ApiSports";

    private static readonly HashSet<string> NotStartedStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Scheduled", "Postponed", "Cancelled" };

    private readonly IGameRepository _gameRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ITeamSeasonRepository _teamSeasonRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IPlayerTeamSeasonRepository _playerTeamSeasonRepository;
    private readonly IStatsRepository _statsRepository;
    private readonly ITeamGameStatsRepository _teamGameStatsRepository;

    public ApiSportsStatsService(
        HttpClient httpClient,
        ILogger<ApiSportsStatsService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        IGameRepository gameRepository,
        ITeamRepository teamRepository,
        ITeamSeasonRepository teamSeasonRepository,
        IPlayerRepository playerRepository,
        IPlayerTeamSeasonRepository playerTeamSeasonRepository,
        IStatsRepository statsRepository,
        ITeamGameStatsRepository teamGameStatsRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _gameRepository = gameRepository;
        _teamRepository = teamRepository;
        _teamSeasonRepository = teamSeasonRepository;
        _playerRepository = playerRepository;
        _playerTeamSeasonRepository = playerTeamSeasonRepository;
        _statsRepository = statsRepository;
        _teamGameStatsRepository = teamGameStatsRepository;
    }

    public async Task<ScrapeResult> ScrapePlayerStatsAsync(int season, int week, NflSeasonType seasonType = NflSeasonType.Regular)
    {
        _logger.LogInformation(
            "Starting player stats scrape for season {Season} week {Week} ({SeasonType}) from api-sports",
            season, week, seasonType);

        var games = (await _gameRepository.GetByWeekAsync(season, week, seasonType)).ToList();
        if (games.Count == 0)
        {
            _logger.LogWarning("No games found for season {Season} week {Week} ({SeasonType}). Scrape games first.", season, week, seasonType);
            return ScrapeResult.Failed($"No games found for season {season} week {week} ({seasonType}). Scrape games first.");
        }

        var apiSportsGames = games.Where(IsApiSportsGame).ToList();
        if (apiSportsGames.Count == 0)
        {
            return ScrapeResult.Failed(
                $"None of the {games.Count} games for season {season} week {week} ({seasonType}) carry an api-sports game id. " +
                $"Run an api-sports games scrape for season {season} first (--source ApiSports).");
        }

        var total = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var game in apiSportsGames)
        {
            if (!HasStarted(game))
            {
                skipped++;
                _logger.LogDebug("Skipping game {RecordId}: status {Status}", game.DataSourceRecordId, game.GameStatus);
                continue;
            }

            try
            {
                var (count, error) = await ScrapeGameAsync(game, season);
                total += count;
                if (error != null)
                    errors.Add(error);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scrape stats for api-sports game {RecordId}", game.DataSourceRecordId);
                errors.Add($"game {game.DataSourceRecordId}: {ex.Message}");
            }
        }

        _logger.LogInformation(
            "api-sports stats scrape complete for season {Season} week {Week}: {Count} stat lines, {Skipped} games not started, {Errors} errors",
            season, week, total, skipped, errors.Count);

        return new ScrapeResult
        {
            Success = errors.Count == 0 || total > 0,
            RecordsProcessed = total,
            RecordsFailed = errors.Count,
            Message = $"{total} stat lines processed for season {season} week {week} ({seasonType}) from api-sports" +
                      (skipped > 0 ? $"; {skipped} game(s) not started yet" : string.Empty),
            Errors = errors
        };
    }

    public static bool IsApiSportsGame(Game game) =>
        string.Equals(game.DataSource, DataSourceName, StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(game.DataSourceRecordId);

    public static bool HasStarted(Game game) =>
        string.IsNullOrWhiteSpace(game.GameStatus) || !NotStartedStatuses.Contains(game.GameStatus.Trim());

    private async Task<(int Count, string? Error)> ScrapeGameAsync(Game game, int season)
    {
        var gameRecordId = game.DataSourceRecordId!;
        var fetchedAt = DateTime.UtcNow;
        var count = 0;

        var (teamBlocks, playersError) = await FetchEnvelopeAsync<ApiSportsPlayerStatsTeamBlock>(
            $"games/statistics/players?id={gameRecordId}");
        if (teamBlocks == null)
            return (0, $"game {gameRecordId}: api-sports error: {playersError}");

        foreach (var block in teamBlocks)
        {
            var resolved = await ResolveTeamAsync(block.Team.Id, season);
            if (resolved == null)
            {
                _logger.LogWarning(
                    "Skipping player stats for api-sports team id {TeamId} in game {RecordId}: team not resolvable",
                    block.Team.Id, gameRecordId);
                continue;
            }

            count += await UpsertPlayerLinesAsync(block, game, resolved.Value.Team, resolved.Value.TeamSeason, season, fetchedAt);
        }

        var (teamStatBlocks, teamsError) = await FetchEnvelopeAsync<ApiSportsTeamStatsBlock>(
            $"games/statistics/teams?id={gameRecordId}");
        if (teamStatBlocks == null)
            return (count, $"game {gameRecordId}: team statistics: api-sports error: {teamsError}");

        foreach (var block in teamStatBlocks)
        {
            var resolved = await ResolveTeamAsync(block.Team.Id, season);
            if (resolved == null)
                continue;

            var tgs = new TeamGameStats
            {
                GameId = game.Id,
                TeamSeasonId = resolved.Value.TeamSeason.Id,
                DataSource = DataSourceName,
                DataSourceRecordId = $"{gameRecordId}:{block.Team.Id}",
                DataSourceFetchedAt = fetchedAt
            };
            ApiSportsStatMappings.MapTeamStatistics(tgs, block.Statistics);

            try
            {
                await _teamGameStatsRepository.UpsertAsync(tgs);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upsert team game stats for game {GameId} teamSeason {TeamSeasonId}", game.Id, tgs.TeamSeasonId);
            }
        }

        return (count, null);
    }

    private async Task<int> UpsertPlayerLinesAsync(
        ApiSportsPlayerStatsTeamBlock block,
        Game game,
        Team team,
        TeamSeason teamSeason,
        int season,
        DateTime fetchedAt)
    {
        // One PlayerGameStats per player, accumulated across the groups the player appears in.
        var lines = new Dictionary<int, ParsedLine>();

        foreach (var group in block.Groups)
        {
            foreach (var statLine in group.Players)
            {
                var apiPlayerId = statLine.Player.Id;
                if (apiPlayerId <= 0)
                    continue;

                if (!lines.TryGetValue(apiPlayerId, out var line))
                {
                    line = new ParsedLine
                    {
                        Name = statLine.Player.Name,
                        Stats = new PlayerGameStats
                        {
                            GameId = game.Id,
                            DataSource = DataSourceName,
                            DataSourceRecordId = $"{game.DataSourceRecordId}:{apiPlayerId}",
                            DataSourceFetchedAt = fetchedAt
                        }
                    };
                    lines[apiPlayerId] = line;
                }

                ApiSportsStatMappings.ApplyPlayerGroup(
                    line.Stats, group.Name, ApiSportsStatMappings.ToValueMap(statLine.Statistics));
            }
        }

        var count = 0;
        foreach (var (apiPlayerId, line) in lines)
        {
            var player = await EnsurePlayerAsync(apiPlayerId, line.Name, team, teamSeason, season, fetchedAt);
            line.Stats.PlayerId = player.Id;
            await _statsRepository.UpsertAsync(line.Stats);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Finds the player by api-sports id or creates a minimal row from the box score. An existing
    /// row is left untouched (roster scrapes own position/height/weight); only the team-season
    /// membership is refreshed.
    /// </summary>
    private async Task<Player> EnsurePlayerAsync(
        int apiPlayerId,
        string? name,
        Team team,
        TeamSeason teamSeason,
        int season,
        DateTime fetchedAt)
    {
        var recordId = apiPlayerId.ToString();
        var player = await _playerRepository.GetByExternalIdAsync(DataSourceName, recordId);

        if (player == null)
        {
            player = await _playerRepository.UpsertByExternalIdAsync(new Player
            {
                Name = string.IsNullOrWhiteSpace(name) ? $"api-sports player {recordId}" : name.Trim(),
                TeamId = team.Id,
                DataSource = DataSourceName,
                DataSourceRecordId = recordId,
                DataSourceFetchedAt = fetchedAt
            });
        }

        await _playerTeamSeasonRepository.UpsertAsync(player.Id, teamSeason.Id, season);
        return player;
    }

    private async Task<(Team Team, TeamSeason TeamSeason)?> ResolveTeamAsync(int apiTeamId, int season)
    {
        var abbreviation = ApiSportsMappings.TeamIdToAbbreviation(apiTeamId);
        if (abbreviation == null)
        {
            _logger.LogWarning("Unknown api-sports team id {TeamId}", apiTeamId);
            return null;
        }

        var team = await _teamRepository.GetByAbbreviationAsync(abbreviation);
        if (team == null)
        {
            _logger.LogWarning("Team {Abbreviation} (api-sports id {TeamId}) not in database. Scrape teams first.", abbreviation, apiTeamId);
            return null;
        }

        var teamSeason = await _teamSeasonRepository.EnsureFromTeamAsync(team, season);
        return (team, teamSeason);
    }

    private sealed class ParsedLine
    {
        public string? Name { get; init; }
        public PlayerGameStats Stats { get; init; } = new();
    }
}
