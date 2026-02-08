namespace WebScraper.Services.Scrapers;

public interface ITeamScraperService
{
    Task ScrapeTeamsAsync();
    Task ScrapeTeamAsync(string abbreviation);
}

public interface IPlayerScraperService
{
    Task ScrapePlayersAsync(int teamId);
    Task ScrapeAllPlayersAsync();
    Task ScrapePlayersAsync(string abbreviation);
    Task ScrapePlayersAsync(string abbreviation, int season);
    Task ScrapeAllPlayersAsync(int season);
}

public interface IGameScraperService
{
    Task ScrapeGamesAsync(int season);
    Task ScrapeGamesAsync(int season, int week);
}

public interface IStatsScraperService
{
    Task ScrapePlayerStatsAsync(int season, int week);
}
