using Microsoft.Extensions.Logging;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Services.Scrapers.Espn;

public class EspnStatsService : BaseApiService, IStatsScraperService
{
    private readonly IStatsRepository _statsRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IGameRepository _gameRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly ITeamSeasonRepository _teamSeasonRepository;
    private readonly IPlayerTeamSeasonRepository _playerTeamSeasonRepository;
    private readonly IVenueRepository _venueRepository;
    private readonly ITeamGameStatsRepository _teamGameStatsRepository;
    private readonly IInjuryRepository _injuryRepository;
    private readonly IApiLinkRepository _apiLinkRepository;
    private readonly IGameDriveRepository _gameDriveRepository;
    private readonly IScoringPlayRepository _scoringPlayRepository;
    private readonly IGameWeatherRepository _gameWeatherRepository;
    private readonly IGameOfficialRepository _gameOfficialRepository;
    private readonly IGameOddsRepository _gameOddsRepository;

    public EspnStatsService(
        HttpClient httpClient,
        ILogger<EspnStatsService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        IStatsRepository statsRepository,
        IPlayerRepository playerRepository,
        IGameRepository gameRepository,
        ITeamRepository teamRepository,
        ITeamSeasonRepository teamSeasonRepository,
        IPlayerTeamSeasonRepository playerTeamSeasonRepository,
        IVenueRepository venueRepository,
        ITeamGameStatsRepository teamGameStatsRepository,
        IInjuryRepository injuryRepository,
        IApiLinkRepository apiLinkRepository,
        IGameDriveRepository gameDriveRepository,
        IScoringPlayRepository scoringPlayRepository,
        IGameWeatherRepository gameWeatherRepository,
        IGameOfficialRepository gameOfficialRepository,
        IGameOddsRepository gameOddsRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _statsRepository = statsRepository;
        _playerRepository = playerRepository;
        _gameRepository = gameRepository;
        _teamRepository = teamRepository;
        _teamSeasonRepository = teamSeasonRepository;
        _playerTeamSeasonRepository = playerTeamSeasonRepository;
        _venueRepository = venueRepository;
        _teamGameStatsRepository = teamGameStatsRepository;
        _injuryRepository = injuryRepository;
        _apiLinkRepository = apiLinkRepository;
        _gameDriveRepository = gameDriveRepository;
        _scoringPlayRepository = scoringPlayRepository;
        _gameWeatherRepository = gameWeatherRepository;
        _gameOfficialRepository = gameOfficialRepository;
        _gameOddsRepository = gameOddsRepository;
    }

    public async Task<ScrapeResult> ScrapePlayerStatsAsync(int season, int week, NflSeasonType seasonType = NflSeasonType.Regular)
    {
        _logger.LogInformation(
            "Starting player stats scrape for season {Season} week {Week} type {SeasonType} from ESPN API",
            season, week, seasonType);

        var games = await _gameRepository.GetByWeekAsync(season, week, seasonType);
        var gamesList = games.ToList();

        if (!gamesList.Any())
        {
            _logger.LogWarning(
                "No games found for season {Season} week {Week} type {SeasonType}. Scrape games first.",
                season, week, seasonType);
            return ScrapeResult.Failed($"No games found for season {season} week {week} ({seasonType}). Scrape games first.");
        }

        if (!EspnGameService.HasEventIdsForWeek(season, week, seasonType))
        {
            _logger.LogInformation(
                "Event ID cache is empty for season {Season} week {Week} type {SeasonType}. Fetching from ESPN API...",
                season, week, seasonType);
            await EspnGameService.PopulateEventIdsAsync(_httpClient, _logger, _rateLimiter, season, week, seasonType);
        }

        int totalStats = 0;
        foreach (var game in gamesList)
        {
            var count = await ScrapeGameStatsAsync(game, season, week, seasonType);
            totalStats += count;
        }

        _logger.LogInformation(
            "Player stats scrape complete for season {Season} week {Week} type {SeasonType}. {Count} stat lines processed",
            season, week, seasonType, totalStats);
        return ScrapeResult.Succeeded(totalStats,
            $"{totalStats} stat lines processed for season {season} week {week} ({seasonType}) from ESPN API");
    }

    private async Task<int> ScrapeGameStatsAsync(Game game, int season, int week, NflSeasonType seasonType)
    {
        var homeTeamSeason = game.HomeTeamSeason ?? await _teamSeasonRepository.GetByIdAsync(game.HomeTeamSeasonId);
        if (homeTeamSeason == null)
        {
            _logger.LogWarning("Home team season not found for game {GameId}", game.Id);
            return 0;
        }

        var eventId = EspnGameService.GetEventId(season, week, homeTeamSeason.Abbreviation, seasonType);
        if (eventId == null)
        {
            _logger.LogWarning(
                "No ESPN event ID found for game {GameId} (season {Season}, week {Week}, home {HomeAbbr}). " +
                "Scrape games first to populate event IDs.",
                game.Id, season, week, homeTeamSeason.Abbreviation);
            return 0;
        }

        var response = await FetchJsonAsync<EspnSummaryResponse>($"/summary?event={eventId}");
        if (response?.Boxscore == null)
        {
            _logger.LogWarning("Failed to fetch box score for event {EventId}", eventId);
            return 0;
        }

        int count = 0;

        // Parse player stats from all categories
        count += await ParsePlayerStatsAsync(response, game);

        // Parse team-level stats
        await ParseTeamStatsAsync(response, game);

        // Extract venue from gameInfo and enrich the game record
        await ExtractVenueAsync(response, game);

        // Extract injuries
        await ExtractInjuriesAsync(response, game);

        // Store API links from header
        await ExtractApiLinksAsync(response, game, season, week, eventId);

        // Tier 1 enrichment: drives, scoring plays, weather, officials, odds, broadcasts
        await ExtractTier1EnrichmentAsync(response, game);

        return count;
    }

    private async Task<int> ParsePlayerStatsAsync(EspnSummaryResponse response, Game game)
    {
        int count = 0;
        foreach (var teamStats in response.Boxscore!.Players)
        {
            var teamAbbr = EspnMappings.ToNflAbbreviation(teamStats.Team.Id, teamStats.Team.Abbreviation);
            var teamSeason = await _teamSeasonRepository.GetByAbbreviationAndSeasonAsync(teamAbbr, game.Season);

            var playerStats = new Dictionary<string, ParsedAthleteStats>();

            foreach (var category in teamStats.Statistics)
            {
                var categoryName = category.Name.ToUpperInvariant();
                Action<PlayerGameStats, List<string>, List<string>>? parser = categoryName switch
                {
                    "PASSING" => ParsePassingStats,
                    "RUSHING" => ParseRushingStats,
                    "RECEIVING" => ParseReceivingStats,
                    "FUMBLES" => ParseFumbleStats,
                    "DEFENSIVE" => ParseDefensiveStats,
                    "INTERCEPTIONS" => ParseInterceptionStats,
                    "KICKRETURNS" => ParseKickReturnStats,
                    "PUNTRETURNS" => ParsePuntReturnStats,
                    "KICKING" => ParseKickingStats,
                    "PUNTING" => ParsePuntingStats,
                    _ => null
                };

                if (parser != null)
                    ParseCategory(category, game.Id, playerStats, parser);
            }

            foreach (var (_, parsed) in playerStats)
            {
                if (string.IsNullOrEmpty(parsed.Athlete.Id))
                    continue;

                var player = await _playerRepository.UpsertByEspnIdAsync(new Player
                {
                    EspnId = parsed.Athlete.Id,
                    Name = parsed.Athlete.DisplayName,
                    TeamId = teamSeason != null
                        ? (await _teamRepository.GetByAbbreviationAsync(teamSeason.Abbreviation))?.Id
                        : null,
                });

                if (teamSeason != null)
                    await _playerTeamSeasonRepository.UpsertAsync(player.Id, teamSeason.Id, game.Season);

                parsed.Stats.PlayerId = player.Id;
                await _statsRepository.UpsertAsync(parsed.Stats);
                count++;
            }
        }
        return count;
    }

    private sealed class ParsedAthleteStats
    {
        public EspnStatAthleteInfo Athlete { get; set; } = new();
        public PlayerGameStats Stats { get; set; } = new();
    }

    private async Task ParseTeamStatsAsync(EspnSummaryResponse response, Game game)
    {
        if (response.Boxscore?.Teams == null) return;

        foreach (var espnTeamStats in response.Boxscore.Teams)
        {
            var teamAbbr = EspnMappings.ToNflAbbreviation(espnTeamStats.Team.Id, espnTeamStats.Team.Abbreviation);
            var teamSeason = await _teamSeasonRepository.GetByAbbreviationAndSeasonAsync(teamAbbr, game.Season);
            if (teamSeason == null)
            {
                _logger.LogDebug("Team season not found for team stats: {TeamAbbr} season {Season}", teamAbbr, game.Season);
                continue;
            }

            var tgs = new TeamGameStats
            {
                GameId = game.Id,
                TeamSeasonId = teamSeason.Id
            };

            foreach (var stat in espnTeamStats.Statistics)
            {
                MapTeamStat(tgs, stat.Name, stat.DisplayValue);
            }

            try
            {
                await _teamGameStatsRepository.UpsertAsync(tgs);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upsert team game stats for game {GameId} teamSeason {TeamSeasonId}", game.Id, teamSeason.Id);
            }
        }
    }

    private static void MapTeamStat(TeamGameStats tgs, string name, string displayValue)
    {
        switch (name.ToUpperInvariant())
        {
            case "FIRSTDOWNS":
                if (int.TryParse(displayValue, out var fd)) tgs.FirstDowns = fd;
                break;
            case "FIRSTDOWNSPASSING":
                if (int.TryParse(displayValue, out var fdp)) tgs.FirstDownsPassing = fdp;
                break;
            case "FIRSTDOWNSRUSHING":
                if (int.TryParse(displayValue, out var fdr)) tgs.FirstDownsRushing = fdr;
                break;
            case "FIRSTDOWNSPENALTY":
                if (int.TryParse(displayValue, out var fdpen)) tgs.FirstDownsPenalty = fdpen;
                break;
            case "THIRDDOWNEFF":
                ParseRatio(displayValue, out var tdm, out var tda);
                tgs.ThirdDownMade = tdm;
                tgs.ThirdDownAttempts = tda;
                break;
            case "FOURTHDOWNEFF":
                ParseRatio(displayValue, out var fodm, out var foda);
                tgs.FourthDownMade = fodm;
                tgs.FourthDownAttempts = foda;
                break;
            case "TOTALOFFENSIVEPLAYS":
                if (int.TryParse(displayValue, out var tp)) tgs.TotalPlays = tp;
                break;
            case "TOTALYARDS":
                if (int.TryParse(displayValue, out var ty)) tgs.TotalYards = ty;
                break;
            case "NETPASSINGYARDS":
                if (int.TryParse(displayValue, out var npy)) tgs.NetPassingYards = npy;
                break;
            case "COMPLETIONATTEMPTS":
                ParseRatio(displayValue, out var pc, out var pa);
                tgs.PassCompletions = pc;
                tgs.PassAttempts = pa;
                break;
            case "YARDSPERPASS":
                if (double.TryParse(displayValue, out var ypp)) tgs.YardsPerPass = ypp;
                break;
            case "INTERCEPTIONS":
                if (int.TryParse(displayValue, out var intVal)) tgs.InterceptionsThrown = intVal;
                break;
            case "SACKSYARDSLOST":
                ParseRatio(displayValue, out var sacks, out var syl);
                tgs.SacksAgainst = sacks;
                tgs.SackYardsLost = syl;
                break;
            case "RUSHINGYARDS":
                if (int.TryParse(displayValue, out var ry)) tgs.RushingYards = ry;
                break;
            case "RUSHINGATTEMPTS":
                if (int.TryParse(displayValue, out var ra)) tgs.RushingAttempts = ra;
                break;
            case "YARDSPERRUSH":
                if (double.TryParse(displayValue, out var ypr)) tgs.YardsPerRush = ypr;
                break;
            case "REDZONEEFF":
                ParseRatio(displayValue, out var rzm, out var rza);
                tgs.RedZoneMade = rzm;
                tgs.RedZoneAttempts = rza;
                break;
            case "TURNOVERS":
                if (int.TryParse(displayValue, out var to)) tgs.Turnovers = to;
                break;
            case "FUMBLESLOST":
                if (int.TryParse(displayValue, out var fl)) tgs.FumblesLost = fl;
                break;
            case "TOTALPENALTIESYARDS":
                ParseRatio(displayValue, out var pen, out var penYds);
                tgs.Penalties = pen;
                tgs.PenaltyYards = penYds;
                break;
            case "DEFENSIVETOUCHDOWNS":
                if (int.TryParse(displayValue, out var dtd)) tgs.DefensiveTouchdowns = dtd;
                break;
            case "POSSESSIONTIME":
                tgs.PossessionTime = displayValue;
                break;
        }
    }

    private static void ParseRatio(string value, out int numerator, out int denominator)
    {
        numerator = 0;
        denominator = 0;
        var parts = value.Split('-', '/');
        if (parts.Length == 2)
        {
            int.TryParse(parts[0], out numerator);
            int.TryParse(parts[1], out denominator);
        }
    }

    private async Task ExtractVenueAsync(EspnSummaryResponse response, Game game)
    {
        var venueData = response.GameInfo?.Venue;
        if (venueData == null || string.IsNullOrEmpty(venueData.Id)) return;

        var venue = new Venue
        {
            EspnId = venueData.Id,
            Name = venueData.FullName,
            City = venueData.Address?.City ?? string.Empty,
            State = venueData.Address?.State ?? string.Empty,
            Country = venueData.Address?.Country ?? string.Empty,
            IsGrass = venueData.Grass,
            IsIndoor = venueData.Indoor
        };

        try
        {
            await _venueRepository.UpsertAsync(venue);

            // Link game to venue if not already set
            if (game.VenueId == null)
            {
                var saved = await _venueRepository.GetByEspnIdAsync(venueData.Id);
                if (saved != null)
                {
                    game.VenueId = saved.Id;
                    game.Attendance = response.GameInfo?.Attendance;
                    await _gameRepository.UpdateAsync(game);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract venue for game {GameId}", game.Id);
        }
    }

    private async Task ExtractInjuriesAsync(EspnSummaryResponse response, Game game)
    {
        if (response.Injuries == null) return;

        foreach (var injuryTeam in response.Injuries)
        {
            foreach (var entry in injuryTeam.Injuries)
            {
                if (string.IsNullOrEmpty(entry.Athlete.Id)) continue;

                // Try to match the player in our database
                var player = await _playerRepository.GetByNameAsync(entry.Athlete.DisplayName);

                DateTime reportDate = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(entry.Date))
                    DateTime.TryParse(entry.Date, out reportDate);

                DateTime? returnDate = null;
                if (!string.IsNullOrEmpty(entry.Details?.ReturnDate))
                {
                    if (DateTime.TryParse(entry.Details.ReturnDate, out var rd))
                        returnDate = rd;
                }

                var injury = new Injury
                {
                    GameId = game.Id,
                    PlayerId = player?.Id,
                    EspnAthleteId = entry.Athlete.Id,
                    PlayerName = entry.Athlete.DisplayName,
                    Status = entry.Status,
                    InjuryType = entry.Details?.Type ?? entry.Type?.Text ?? string.Empty,
                    BodyLocation = entry.Details?.Location ?? string.Empty,
                    Side = entry.Details?.Side ?? string.Empty,
                    Detail = entry.Details?.Detail ?? string.Empty,
                    ReturnDate = returnDate,
                    ReportDate = reportDate
                };

                try
                {
                    await _injuryRepository.UpsertAsync(injury);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to upsert injury for athlete {AthleteId} in game {GameId}",
                        entry.Athlete.Id, game.Id);
                }
            }
        }
    }

    private async Task ExtractApiLinksAsync(EspnSummaryResponse response, Game game, int season, int week, string eventId)
    {
        if (response.Header?.Links == null) return;

        foreach (var link in response.Header.Links)
        {
            if (string.IsNullOrEmpty(link.Href)) continue;

            var relationType = link.Rel?.FirstOrDefault() ?? link.Text;

            try
            {
                var apiLink = new ApiLink
                {
                    Url = link.Href,
                    EndpointType = "summary",
                    RelationType = relationType,
                    GameId = game.Id,
                    Season = season,
                    Week = week,
                    EspnEventId = eventId,
                    DiscoveredAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow
                };
                await _apiLinkRepository.UpsertAsync(apiLink);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to store API link: {Url}", link.Href);
            }
        }
    }

    private async Task ExtractTier1EnrichmentAsync(EspnSummaryResponse response, Game game)
    {
        await ExtractDrivesAsync(response, game);
        await ExtractScoringPlaysAsync(response, game);
        await ExtractWeatherAsync(response, game);
        await ExtractOfficialsAsync(response, game);
        await ExtractOddsAsync(response, game);
        await ExtractSummaryBroadcastsAsync(response, game);
    }

    private async Task ExtractDrivesAsync(EspnSummaryResponse response, Game game)
    {
        var espnDrives = new List<EspnDrive>();
        if (response.Drives?.Previous != null)
            espnDrives.AddRange(response.Drives.Previous);
        if (response.Drives?.Current != null)
            espnDrives.Add(response.Drives.Current);

        if (espnDrives.Count == 0)
            return;

        var drives = new List<GameDrive>();
        var sequence = 0;
        foreach (var d in espnDrives)
        {
            if (string.IsNullOrEmpty(d.Id))
                continue;

            int? teamSeasonId = null;
            if (d.Team != null && !string.IsNullOrEmpty(d.Team.Abbreviation))
            {
                var teamSeason = await _teamSeasonRepository.GetByAbbreviationAndSeasonAsync(
                    d.Team.Abbreviation, game.Season);
                teamSeasonId = teamSeason?.Id;
            }

            drives.Add(new GameDrive
            {
                GameId = game.Id,
                EspnDriveId = d.Id,
                Sequence = sequence++,
                TeamSeasonId = teamSeasonId,
                Description = d.Description,
                StartPeriod = d.Start?.Period?.Number,
                EndPeriod = d.End?.Period?.Number,
                TimeElapsed = d.TimeElapsed?.DisplayValue,
                Yards = d.Yards,
                OffensivePlays = d.OffensivePlays,
                IsScore = d.IsScore,
                Result = d.Result,
                DisplayResult = d.DisplayResult,
                DataSource = "Espn",
                DataSourceFetchedAt = DateTime.UtcNow,
                DataSourceRecordId = d.Id,
            });
        }

        if (drives.Count == 0)
            return;

        try
        {
            await _gameDriveRepository.ReplaceForGameAsync(game.Id, drives);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to store drives for game {GameId}", game.Id);
        }
    }

    private async Task ExtractScoringPlaysAsync(EspnSummaryResponse response, Game game)
    {
        if (response.ScoringPlays == null || response.ScoringPlays.Count == 0)
            return;

        var plays = new List<ScoringPlay>();
        var sequence = 0;
        foreach (var p in response.ScoringPlays)
        {
            if (string.IsNullOrEmpty(p.Id))
                continue;

            int? teamSeasonId = null;
            if (p.Team != null && !string.IsNullOrEmpty(p.Team.Abbreviation))
            {
                var teamSeason = await _teamSeasonRepository.GetByAbbreviationAndSeasonAsync(
                    p.Team.Abbreviation, game.Season);
                teamSeasonId = teamSeason?.Id;
            }

            plays.Add(new ScoringPlay
            {
                GameId = game.Id,
                EspnPlayId = p.Id,
                Sequence = sequence++,
                TeamSeasonId = teamSeasonId,
                Period = p.Period?.Number ?? 0,
                Clock = p.Clock?.DisplayValue,
                PlayType = p.Type?.Text ?? string.Empty,
                Description = p.Text,
                HomeScore = p.HomeScore,
                AwayScore = p.AwayScore,
                ScoringType = p.ScoringType?.Text ?? p.Type?.Abbreviation,
                DataSource = "Espn",
                DataSourceFetchedAt = DateTime.UtcNow,
                DataSourceRecordId = p.Id,
            });
        }

        if (plays.Count == 0)
            return;

        try
        {
            await _scoringPlayRepository.ReplaceForGameAsync(game.Id, plays);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to store scoring plays for game {GameId}", game.Id);
        }
    }

    private async Task ExtractWeatherAsync(EspnSummaryResponse response, Game game)
    {
        var w = response.GameInfo?.Weather;
        if (w == null)
            return;

        var weather = new GameWeather
        {
            GameId = game.Id,
            TemperatureF = w.Temperature,
            HighTemperatureF = w.HighTemperature,
            Condition = w.DisplayValue,
            WindSpeedMph = w.WindSpeed,
            WindDirection = w.WindDirection,
            HumidityPercent = w.Humidity,
            DataSource = "Espn",
            DataSourceFetchedAt = DateTime.UtcNow,
        };

        try
        {
            await _gameWeatherRepository.UpsertAsync(weather);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to store weather for game {GameId}", game.Id);
        }
    }

    private async Task ExtractOfficialsAsync(EspnSummaryResponse response, Game game)
    {
        if (response.GameInfo?.Officials == null || response.GameInfo.Officials.Count == 0)
            return;

        var officials = response.GameInfo.Officials
            .Select((o, index) => new GameOfficial
            {
                GameId = game.Id,
                Name = !string.IsNullOrWhiteSpace(o.FullName) ? o.FullName : o.DisplayName,
                Position = o.Position?.DisplayName ?? string.Empty,
                SortOrder = o.Order > 0 ? o.Order : index,
                DataSource = "Espn",
                DataSourceFetchedAt = DateTime.UtcNow,
            })
            .Where(o => !string.IsNullOrWhiteSpace(o.Name))
            .ToList();

        if (officials.Count == 0)
            return;

        try
        {
            await _gameOfficialRepository.ReplaceForGameAsync(game.Id, officials);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to store officials for game {GameId}", game.Id);
        }
    }

    private async Task ExtractOddsAsync(EspnSummaryResponse response, Game game)
    {
        if (response.Pickcenter == null || response.Pickcenter.Count == 0)
            return;

        var capturedAt = DateTime.UtcNow;
        foreach (var pick in response.Pickcenter)
        {
            var sportsbook = pick.Provider?.Name;
            if (string.IsNullOrWhiteSpace(sportsbook))
                sportsbook = "ESPN";

            var odds = new GameOdds
            {
                GameId = game.Id,
                Sportsbook = sportsbook,
                Spread = pick.Spread,
                OverUnder = pick.OverUnder,
                HomeMoneyline = pick.HomeTeamOdds?.MoneyLine,
                AwayMoneyline = pick.AwayTeamOdds?.MoneyLine,
                SnapshotType = OddsSnapshotType.Current,
                CapturedAt = capturedAt,
                Details = pick.Details,
                DataSource = "Espn",
                DataSourceFetchedAt = capturedAt,
            };

            try
            {
                await _gameOddsRepository.AddSnapshotAsync(odds);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to store odds for game {GameId} sportsbook {Sportsbook}",
                    game.Id, sportsbook);
            }
        }
    }

    private async Task ExtractSummaryBroadcastsAsync(EspnSummaryResponse response, Game game)
    {
        if (response.Broadcasts == null || response.Broadcasts.Count == 0)
            return;

        var names = response.Broadcasts
            .Select(b => b.Station ?? b.Media?.ShortName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count == 0)
            return;

        game.BroadcastNetworks = string.Join(", ", names);
        try
        {
            await _gameRepository.UpdateAsync(game);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to update broadcast networks for game {GameId}", game.Id);
        }
    }

    private static void ParseCategory(
        EspnStatCategory category,
        int gameId,
        Dictionary<string, ParsedAthleteStats> playerStats,
        Action<PlayerGameStats, List<string>, List<string>> parser)
    {
        foreach (var athlete in category.Athletes)
        {
            var espnId = athlete.Athlete.Id;
            var name = athlete.Athlete.DisplayName;
            if (string.IsNullOrEmpty(espnId) || string.IsNullOrEmpty(name))
                continue;

            if (!playerStats.TryGetValue(espnId, out var parsed))
            {
                parsed = new ParsedAthleteStats
                {
                    Athlete = athlete.Athlete,
                    Stats = new PlayerGameStats { GameId = gameId },
                };
                playerStats[espnId] = parsed;
            }

            parser(parsed.Stats, category.Keys, athlete.Stats);
        }
    }

    private static void ParsePassingStats(PlayerGameStats stats, List<string> keys, List<string> values)
    {
        for (int i = 0; i < keys.Count && i < values.Count; i++)
        {
            var value = values[i];
            switch (keys[i].ToUpperInvariant())
            {
                case "C/ATT":
                case "COMPLETIONS/PASSINGATTEMPTS":
                    var parts = value.Split('/');
                    if (parts.Length == 2)
                    {
                        if (int.TryParse(parts[0], out var cmp)) stats.PassCompletions = cmp;
                        if (int.TryParse(parts[1], out var att)) stats.PassAttempts = att;
                    }
                    break;
                case "YDS":
                case "PASSINGYARDS":
                    if (int.TryParse(value, out var yds)) stats.PassYards = yds;
                    break;
                case "TD":
                case "PASSINGTOUCHDOWNS":
                    if (int.TryParse(value, out var td)) stats.PassTouchdowns = td;
                    break;
                case "INT":
                case "INTERCEPTIONS":
                    if (int.TryParse(value, out var ints)) stats.Interceptions = ints;
                    break;
                case "QBR":
                    if (double.TryParse(value, out var qbr)) stats.QBRating = qbr;
                    break;
                case "RTG":
                    if (double.TryParse(value, out var rtg)) stats.QBRating = rtg;
                    break;
                case "SACKS":
                case "SACKS-YARDSLOST":
                    var sackParts = value.Split('-');
                    if (sackParts.Length == 2)
                    {
                        if (int.TryParse(sackParts[0], out var sk)) stats.Sacks = sk;
                        if (int.TryParse(sackParts[1], out var syl)) stats.SackYardsLost = syl;
                    }
                    else if (int.TryParse(value, out var sackVal))
                    {
                        stats.Sacks = sackVal;
                    }
                    break;
            }
        }
    }

    private static void ParseRushingStats(PlayerGameStats stats, List<string> keys, List<string> values)
    {
        for (int i = 0; i < keys.Count && i < values.Count; i++)
        {
            var value = values[i];
            switch (keys[i].ToUpperInvariant())
            {
                case "CAR":
                case "RUSHINGATTEMPTS":
                    if (int.TryParse(value, out var att)) stats.RushAttempts = att;
                    break;
                case "YDS":
                case "RUSHINGYARDS":
                    if (int.TryParse(value, out var yds)) stats.RushYards = yds;
                    break;
                case "TD":
                case "RUSHINGTOUCHDOWNS":
                    if (int.TryParse(value, out var td)) stats.RushTouchdowns = td;
                    break;
                case "LONG":
                case "LONGESTRUSHING":
                    if (int.TryParse(value, out var lng)) stats.LongRushing = lng;
                    break;
            }
        }
    }

    private static void ParseReceivingStats(PlayerGameStats stats, List<string> keys, List<string> values)
    {
        for (int i = 0; i < keys.Count && i < values.Count; i++)
        {
            var value = values[i];
            switch (keys[i].ToUpperInvariant())
            {
                case "REC":
                case "RECEPTIONS":
                    if (int.TryParse(value, out var rec)) stats.Receptions = rec;
                    break;
                case "YDS":
                case "RECEIVINGYARDS":
                    if (int.TryParse(value, out var yds)) stats.ReceivingYards = yds;
                    break;
                case "TD":
                case "RECEIVINGTOUCHDOWNS":
                    if (int.TryParse(value, out var td)) stats.ReceivingTouchdowns = td;
                    break;
                case "TGTS":
                case "TARGETS":
                case "RECEIVINGTARGETS":
                    if (int.TryParse(value, out var tgt)) stats.ReceivingTargets = tgt;
                    break;
                case "LONG":
                case "LONGESTRECEPTION":
                    if (int.TryParse(value, out var lng)) stats.LongReception = lng;
                    break;
                case "AVG":
                case "YARDSPERRECEPTION":
                    if (double.TryParse(value, out var avg)) stats.YardsPerReception = avg;
                    break;
            }
        }
    }

    private static void ParseFumbleStats(PlayerGameStats stats, List<string> keys, List<string> values)
    {
        for (int i = 0; i < keys.Count && i < values.Count; i++)
        {
            var value = values[i];
            switch (keys[i].ToUpperInvariant())
            {
                case "FUM":
                case "FUMBLES":
                    if (int.TryParse(value, out var fum)) stats.Fumbles = fum;
                    break;
                case "LOST":
                case "FUMBLESLOST":
                    if (int.TryParse(value, out var lost)) stats.FumblesLost = lost;
                    break;
                case "REC":
                case "FUMBLESRECOVERED":
                    if (int.TryParse(value, out var rec)) stats.FumblesRecovered = rec;
                    break;
            }
        }
    }

    private static void ParseDefensiveStats(PlayerGameStats stats, List<string> keys, List<string> values)
    {
        for (int i = 0; i < keys.Count && i < values.Count; i++)
        {
            var value = values[i];
            switch (keys[i].ToUpperInvariant())
            {
                case "TOT":
                case "TOTALTACKLES":
                    if (int.TryParse(value, out var tot)) stats.TotalTackles = tot;
                    break;
                case "SOLO":
                case "SOLOTACKLES":
                    if (int.TryParse(value, out var solo)) stats.SoloTackles = solo;
                    break;
                case "SACKS":
                    if (double.TryParse(value, out var sacks)) stats.DefensiveSacks = sacks;
                    break;
                case "TFL":
                case "TACKLESFORLOSS":
                    if (int.TryParse(value, out var tfl)) stats.TacklesForLoss = tfl;
                    break;
                case "PD":
                case "PASSESDEFENDED":
                    if (int.TryParse(value, out var pd)) stats.PassesDefended = pd;
                    break;
                case "QH":
                case "QBHITS":
                    if (int.TryParse(value, out var qh)) stats.QBHits = qh;
                    break;
                case "TD":
                case "DEFENSIVETOUCHDOWNS":
                    if (int.TryParse(value, out var td)) stats.DefensiveTouchdowns = td;
                    break;
            }
        }
    }

    private static void ParseInterceptionStats(PlayerGameStats stats, List<string> keys, List<string> values)
    {
        for (int i = 0; i < keys.Count && i < values.Count; i++)
        {
            var value = values[i];
            switch (keys[i].ToUpperInvariant())
            {
                case "INT":
                case "INTERCEPTIONS":
                    if (int.TryParse(value, out var intVal)) stats.InterceptionsCaught = intVal;
                    break;
                case "YDS":
                case "INTERCEPTIONYARDS":
                    if (int.TryParse(value, out var yds)) stats.InterceptionYards = yds;
                    break;
                case "TD":
                case "INTERCEPTIONTOUCHDOWNS":
                    if (int.TryParse(value, out var td)) stats.InterceptionTouchdowns = td;
                    break;
            }
        }
    }

    private static void ParseKickReturnStats(PlayerGameStats stats, List<string> keys, List<string> values)
    {
        for (int i = 0; i < keys.Count && i < values.Count; i++)
        {
            var value = values[i];
            switch (keys[i].ToUpperInvariant())
            {
                case "NO":
                case "KICKRETURNS":
                    if (int.TryParse(value, out var no)) stats.KickReturns = no;
                    break;
                case "YDS":
                case "KICKRETURNYARDS":
                    if (int.TryParse(value, out var yds)) stats.KickReturnYards = yds;
                    break;
                case "LONG":
                case "LONGESTKICKRETURN":
                    if (int.TryParse(value, out var lng)) stats.LongKickReturn = lng;
                    break;
                case "TD":
                case "KICKRETURNTOUCHDOWNS":
                    if (int.TryParse(value, out var td)) stats.KickReturnTouchdowns = td;
                    break;
            }
        }
    }

    private static void ParsePuntReturnStats(PlayerGameStats stats, List<string> keys, List<string> values)
    {
        for (int i = 0; i < keys.Count && i < values.Count; i++)
        {
            var value = values[i];
            switch (keys[i].ToUpperInvariant())
            {
                case "NO":
                case "PUNTRETURNS":
                    if (int.TryParse(value, out var no)) stats.PuntReturns = no;
                    break;
                case "YDS":
                case "PUNTRETURNYARDS":
                    if (int.TryParse(value, out var yds)) stats.PuntReturnYards = yds;
                    break;
                case "LONG":
                case "LONGESTPUNTRETURN":
                    if (int.TryParse(value, out var lng)) stats.LongPuntReturn = lng;
                    break;
                case "TD":
                case "PUNTRETURNTOUCHDOWNS":
                    if (int.TryParse(value, out var td)) stats.PuntReturnTouchdowns = td;
                    break;
            }
        }
    }

    private static void ParseKickingStats(PlayerGameStats stats, List<string> keys, List<string> values)
    {
        for (int i = 0; i < keys.Count && i < values.Count; i++)
        {
            var value = values[i];
            switch (keys[i].ToUpperInvariant())
            {
                case "FG":
                case "FIELDGOALS":
                    var fgParts = value.Split('/');
                    if (fgParts.Length == 2)
                    {
                        if (int.TryParse(fgParts[0], out var fgm)) stats.FieldGoalsMade = fgm;
                        if (int.TryParse(fgParts[1], out var fga)) stats.FieldGoalAttempts = fga;
                    }
                    break;
                case "LONG":
                case "LONGESTFIELDGOALMADE":
                    if (int.TryParse(value, out var lng)) stats.LongFieldGoal = lng;
                    break;
                case "XP":
                case "EXTRAPOINTS":
                    var xpParts = value.Split('/');
                    if (xpParts.Length == 2)
                    {
                        if (int.TryParse(xpParts[0], out var xpm)) stats.ExtraPointsMade = xpm;
                        if (int.TryParse(xpParts[1], out var xpa)) stats.ExtraPointAttempts = xpa;
                    }
                    break;
                case "PTS":
                case "TOTALKICKINGPOINTS":
                    if (int.TryParse(value, out var pts)) stats.TotalKickingPoints = pts;
                    break;
            }
        }
    }

    private static void ParsePuntingStats(PlayerGameStats stats, List<string> keys, List<string> values)
    {
        for (int i = 0; i < keys.Count && i < values.Count; i++)
        {
            var value = values[i];
            switch (keys[i].ToUpperInvariant())
            {
                case "NO":
                case "PUNTS":
                    if (int.TryParse(value, out var no)) stats.Punts = no;
                    break;
                case "YDS":
                case "PUNTYARDS":
                    if (int.TryParse(value, out var yds)) stats.PuntYards = yds;
                    break;
                case "AVG":
                case "GROSSAVGPUNTYARDS":
                    if (double.TryParse(value, out var avg)) stats.GrossAvgPuntYards = avg;
                    break;
                case "TB":
                case "TOUCHBACKS":
                    if (int.TryParse(value, out var tb)) stats.PuntTouchbacks = tb;
                    break;
                case "IN 20":
                case "INSIDE20":
                case "PUNTSINSIDE20":
                    if (int.TryParse(value, out var in20)) stats.PuntsInside20 = in20;
                    break;
                case "LONG":
                case "LONGESTPUNT":
                    if (int.TryParse(value, out var lng)) stats.LongPunt = lng;
                    break;
            }
        }
    }
}
