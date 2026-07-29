using WebScraper.Models;

namespace WebScraper.Data.Repositories;

public interface IGameDriveRepository
{
    Task<IReadOnlyList<GameDrive>> GetByGameAsync(int gameId);
    Task ReplaceForGameAsync(int gameId, IEnumerable<GameDrive> drives);
}

public interface IScoringPlayRepository
{
    Task<IReadOnlyList<ScoringPlay>> GetByGameAsync(int gameId);
    Task ReplaceForGameAsync(int gameId, IEnumerable<ScoringPlay> plays);
}

public interface IGameWeatherRepository
{
    Task<GameWeather?> GetByGameAsync(int gameId);
    Task UpsertAsync(GameWeather weather);
}

public interface IGameOfficialRepository
{
    Task<IReadOnlyList<GameOfficial>> GetByGameAsync(int gameId);
    Task ReplaceForGameAsync(int gameId, IEnumerable<GameOfficial> officials);
}

public interface IGameOddsRepository
{
    Task<IReadOnlyList<GameOdds>> GetByGameAsync(int gameId);
    Task AddSnapshotAsync(GameOdds odds);
}
