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
    private readonly IVenueRepository _venueRepository;
    private readonly ITeamGameStatsRepository _teamGameStatsRepository;
    private readonly IInjuryRepository _injuryRepository;
    private readonly IApiLinkRepository _apiLinkRepository;

    public EspnStatsService(
        HttpClient httpClient,
        ILogger<EspnStatsService> logger,
        ApiProviderSettings providerSettings,
        RateLimiterService rateLimiter,
        IStatsRepository statsRepository,
        IPlayerRepository playerRepository,
        IGameRepository gameRepository,
        ITeamRepository teamRepository,
        IVenueRepository venueRepository,
        ITeamGameStatsRepository teamGameStatsRepository,
        IInjuryRepository injuryRepository,
        IApiLinkRepository apiLinkRepository)
        : base(httpClient, logger, providerSettings, rateLimiter)
    {
        _statsRepository = statsRepository;
        _playerRepository = playerRepository;
        _gameRepository = gameRepository;
        _teamRepository = teamRepository;
        _venueRepository = venueRepository;
        _teamGameStatsRepository = teamGameStatsRepository;
        _injuryRepository = injuryRepository;
        _apiLinkRepository = apiLinkRepository;
    }

    public async Task<ScrapeResult> ScrapePlayerStatsAsync(int season, int week)
    {
        _logger.LogInformation("Starting player stats scrape for season {Season} week {Week} from ESPN API", season, week);

        var games = await _gameRepository.GetByWeekAsync(season, week);
        var gamesList = games.ToList();

        if (!gamesList.Any())
        {
            _logger.LogWarning("No games found for season {Season} week {Week}. Scrape games first.", season, week);
            return ScrapeResult.Failed($"No games found for season {season} week {week}. Scrape games first.");
        }

        if (!EspnGameService.HasEventIdsForWeek(season, week))
        {
            _logger.LogInformation("Event ID cache is empty for season {Season} week {Week}. Fetching from ESPN API...", season, week);
            await EspnGameService.PopulateEventIdsAsync(_httpClient, _logger, _rateLimiter, season, week);
        }

        int totalStats = 0;
        foreach (var game in gamesList)
        {
            var count = await ScrapeGameStatsAsync(game, season, week);
            totalStats += count;
        }

        _logger.LogInformation("Player stats scrape complete for season {Season} week {Week}. {Count} stat lines processed",
            season, week, totalStats);
        return ScrapeResult.Succeeded(totalStats, $"{totalStats} stat lines processed for season {season} week {week} from ESPN API");
    }

    private async Task<int> ScrapeGameStatsAsync(Game game, int season, int week)
    {
        var homeTeam = game.HomeTeam ?? await _teamRepository.GetByIdAsync(game.HomeTeamId);
        if (homeTeam == null)
        {
            _logger.LogWarning("Home team not found for game {GameId}", game.Id);
            return 0;
        }

        var eventId = EspnGameService.GetEventId(season, week, homeTeam.Abbreviation);
        if (eventId == null)
        {
            _logger.LogWarning(
                "No ESPN event ID found for game {GameId} (season {Season}, week {Week}, home {HomeAbbr}). " +
                "Scrape games first to populate event IDs.",
                game.Id, season, week, homeTeam.Abbreviation);
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

        return count;
    }

    private async Task<int> ParsePlayerStatsAsync(EspnSummaryResponse response, Game game)
    {
        int count = 0;
        foreach (var teamStats in response.Boxscore!.Players)
        {
            var playerStats = new Dictionary<string, PlayerGameStats>();

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

            foreach (var (playerName, stats) in playerStats)
            {
                var player = await _playerRepository.GetByNameAsync(playerName);
                if (player == null)
                {
                    _logger.LogDebug("Player not found in database: {PlayerName}. Skipping.", playerName);
                    continue;
                }

                stats.PlayerId = player.Id;
                await _statsRepository.UpsertAsync(stats);
                count++;
            }
        }
        return count;
    }

    private async Task ParseTeamStatsAsync(EspnSummaryResponse response, Game game)
    {
        if (response.Boxscore?.Teams == null) return;

        foreach (var espnTeamStats in response.Boxscore.Teams)
        {
            var teamAbbr = EspnMappings.ToNflAbbreviation(espnTeamStats.Team.Id, espnTeamStats.Team.Abbreviation);
            var team = await _teamRepository.GetByAbbreviationAsync(teamAbbr);
            if (team == null)
            {
                _logger.LogDebug("Team not found for team stats: {TeamAbbr}", teamAbbr);
                continue;
            }

            var tgs = new TeamGameStats
            {
                GameId = game.Id,
                TeamId = team.Id
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
                _logger.LogWarning(ex, "Failed to upsert team game stats for game {GameId} team {TeamId}", game.Id, team.Id);
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

    private static void ParseCategory(
        EspnStatCategory category,
        int gameId,
        Dictionary<string, PlayerGameStats> playerStats,
        Action<PlayerGameStats, List<string>, List<string>> parser)
    {
        foreach (var athlete in category.Athletes)
        {
            var name = athlete.Athlete.DisplayName;
            if (string.IsNullOrEmpty(name)) continue;

            if (!playerStats.TryGetValue(name, out var stats))
            {
                stats = new PlayerGameStats { GameId = gameId };
                playerStats[name] = stats;
            }

            parser(stats, category.Keys, athlete.Stats);
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
