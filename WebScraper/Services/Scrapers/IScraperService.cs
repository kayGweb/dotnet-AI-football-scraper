using WebScraper.Models;

namespace WebScraper.Services.Scrapers;

public interface ITeamScraperService
{
    Task<ScrapeResult> ScrapeTeamsAsync();
    Task<ScrapeResult> ScrapeTeamAsync(string abbreviation);
}

public interface IPlayerScraperService
{
    Task<ScrapeResult> ScrapePlayersAsync(int teamId);
    Task<ScrapeResult> ScrapeAllPlayersAsync();
}

public interface IGameScraperService
{
    Task<ScrapeResult> ScrapeGamesAsync(int season);
    Task<ScrapeResult> ScrapeGamesAsync(int season, int week);
}

public interface IStatsScraperService
{
    Task<ScrapeResult> ScrapePlayerStatsAsync(int season, int week);
}
