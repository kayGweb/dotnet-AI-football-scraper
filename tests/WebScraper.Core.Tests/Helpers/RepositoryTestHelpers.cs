using WebScraper.Data;
using WebScraper.Data.Repositories;
using WebScraper.Models;

namespace WebScraper.Tests.Helpers;

public static class RepositoryTestHelpers
{
    public static async Task<(TeamSeason home, TeamSeason away, Team homeTeam, Team awayTeam)> SeedTeamSeasonsAsync(
        AppDbContext context, int season = 2025)
    {
        var teamRepo = new TeamRepository(context);
        var franchiseRepo = new FranchiseRepository(context);
        var teamSeasonRepo = new TeamSeasonRepository(context, franchiseRepo);

        var homeTeam = await teamRepo.AddAsync(new Team
        {
            Name = "Kansas City Chiefs", Abbreviation = "KC",
            City = "Kansas City", Conference = "AFC", Division = "West"
        });
        var awayTeam = await teamRepo.AddAsync(new Team
        {
            Name = "Buffalo Bills", Abbreviation = "BUF",
            City = "Buffalo", Conference = "AFC", Division = "East"
        });

        var homeSeason = await teamSeasonRepo.EnsureFromTeamAsync(homeTeam, season);
        var awaySeason = await teamSeasonRepo.EnsureFromTeamAsync(awayTeam, season);
        return (homeSeason, awaySeason, homeTeam, awayTeam);
    }
}
