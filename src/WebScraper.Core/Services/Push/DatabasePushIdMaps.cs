using Microsoft.EntityFrameworkCore;
using WebScraper.Data;
using WebScraper.Models;

namespace WebScraper.Services.Push;

/// <summary>
/// Local-to-remote primary key maps rebuilt from natural keys when resuming a push.
/// </summary>
public sealed class DatabasePushIdMaps
{
    public Dictionary<int, int> TeamIdMap { get; } = new();
    public Dictionary<int, int> FranchiseIdMap { get; } = new();
    public Dictionary<int, int> TeamSeasonIdMap { get; } = new();
    public Dictionary<int, int> PlayerIdMap { get; } = new();
    public Dictionary<int, int> VenueIdMap { get; } = new();
    public Dictionary<int, int> GameIdMap { get; } = new();

    public static async Task<DatabasePushIdMaps> RebuildAsync(AppDbContext localDb, AppDbContext remoteDb)
    {
        var maps = new DatabasePushIdMaps();

        var localTeams = await localDb.Teams.AsNoTracking().ToListAsync();
        var remoteTeams = await remoteDb.Teams.AsNoTracking().ToListAsync();
        foreach (var local in localTeams)
        {
            var remote = remoteTeams.FirstOrDefault(t => t.Abbreviation == local.Abbreviation);
            if (remote != null)
                maps.TeamIdMap[local.Id] = remote.Id;
        }

        var localFranchises = await localDb.Franchises.AsNoTracking().ToListAsync();
        var remoteFranchises = await remoteDb.Franchises.AsNoTracking().ToListAsync();
        foreach (var local in localFranchises)
        {
            var remote = remoteFranchises.FirstOrDefault(f => f.CanonicalAbbreviation == local.CanonicalAbbreviation);
            if (remote != null)
                maps.FranchiseIdMap[local.Id] = remote.Id;
        }

        var localTeamSeasons = await localDb.TeamSeasons.AsNoTracking().ToListAsync();
        var remoteTeamSeasons = await remoteDb.TeamSeasons.AsNoTracking().ToListAsync();
        foreach (var local in localTeamSeasons)
        {
            if (!maps.FranchiseIdMap.TryGetValue(local.FranchiseId, out var remoteFranchiseId))
                continue;

            var remote = remoteTeamSeasons.FirstOrDefault(ts =>
                ts.FranchiseId == remoteFranchiseId && ts.Season == local.Season);
            if (remote != null)
                maps.TeamSeasonIdMap[local.Id] = remote.Id;
        }

        var localPlayers = await localDb.Players.AsNoTracking().ToListAsync();
        var remotePlayers = await remoteDb.Players.AsNoTracking().ToListAsync();
        foreach (var local in localPlayers)
        {
            int? remoteTeamId = local.TeamId.HasValue && maps.TeamIdMap.TryGetValue(local.TeamId.Value, out var tid)
                ? tid
                : null;

            Player? remote = null;
            if (!string.IsNullOrEmpty(local.EspnId))
                remote = remotePlayers.FirstOrDefault(p => p.EspnId == local.EspnId);
            remote ??= remotePlayers.FirstOrDefault(p => p.Name == local.Name && p.TeamId == remoteTeamId);

            if (remote != null)
                maps.PlayerIdMap[local.Id] = remote.Id;
        }

        var localVenues = await localDb.Venues.AsNoTracking().ToListAsync();
        var remoteVenues = await remoteDb.Venues.AsNoTracking().ToListAsync();
        foreach (var local in localVenues)
        {
            var remote = remoteVenues.FirstOrDefault(v => v.EspnId == local.EspnId);
            if (remote != null)
                maps.VenueIdMap[local.Id] = remote.Id;
        }

        var localGames = await localDb.Games.AsNoTracking().ToListAsync();
        var remoteGames = await remoteDb.Games.AsNoTracking().ToListAsync();
        foreach (var local in localGames)
        {
            if (!maps.TeamSeasonIdMap.TryGetValue(local.HomeTeamSeasonId, out var remoteHomeId)
                || !maps.TeamSeasonIdMap.TryGetValue(local.AwayTeamSeasonId, out var remoteAwayId))
                continue;

            var remote = remoteGames.FirstOrDefault(g =>
                g.Season == local.Season
                && g.SeasonType == local.SeasonType
                && g.Week == local.Week
                && g.HomeTeamSeasonId == remoteHomeId
                && g.AwayTeamSeasonId == remoteAwayId);

            if (remote != null)
                maps.GameIdMap[local.Id] = remote.Id;
        }

        return maps;
    }
}
