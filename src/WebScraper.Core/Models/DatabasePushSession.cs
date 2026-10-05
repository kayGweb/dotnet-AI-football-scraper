namespace WebScraper.Models;

/// <summary>
/// Checkpoint for incremental/resumable SQLite → PostgreSQL push (Phase B / §7).
/// Stored in the local database so progress survives process restarts.
/// </summary>
public class DatabasePushSession
{
    public int Id { get; set; }

    public PushSessionStatus Status { get; set; } = PushSessionStatus.InProgress;

    public PushStage CurrentStage { get; set; } = PushStage.MigrateSchema;

    /// <summary>Records already processed within <see cref="CurrentStage"/> (for batched stages).</summary>
    public int StageOffset { get; set; }

    public long TotalRecordsPushed { get; set; }

    public string? LastError { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

public enum PushSessionStatus
{
    InProgress = 1,
    Completed = 2,
    Failed = 3,
}

public enum PushStage
{
    MigrateSchema = 0,
    Teams = 1,
    Franchises = 2,
    TeamSeasons = 3,
    Players = 4,
    Venues = 5,
    Games = 6,
    PlayerGameStats = 7,
    TeamGameStats = 8,
    Injuries = 9,
    InjuryReports = 10,
    ApiLinks = 11,
    GameDrives = 12,
    ScoringPlays = 13,
    GameWeather = 14,
    GameOfficials = 15,
    GameOdds = 16,
    Done = 17,
}
