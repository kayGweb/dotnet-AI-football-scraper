using WebScraper.Models;
using WebScraper.Services.Coverage;

namespace WebScraper.Api.Dtos.Admin;

public class StartBackfillRequest
{
    public int StartSeason { get; set; }
    public int EndSeason { get; set; }
    public bool BackupFirst { get; set; }
}

public class BackfillProgressDto
{
    public int ParentJobId { get; set; }
    public string ParentStatus { get; set; } = string.Empty;
    public int StartSeason { get; set; }
    public int EndSeason { get; set; }
    public int TotalChildJobs { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Running { get; set; }
    public int Queued { get; set; }
    public int Paused { get; set; }
    public double PercentComplete { get; set; }
    public string? EstimatedRemaining { get; set; }
    public BackfillWorkloadDto? WorkloadEstimate { get; set; }

    public static BackfillProgressDto From(BackfillProgress progress, BackfillWorkloadEstimate? estimate = null) => new()
    {
        ParentJobId = progress.ParentJobId,
        ParentStatus = progress.ParentStatus,
        StartSeason = progress.StartSeason,
        EndSeason = progress.EndSeason,
        TotalChildJobs = progress.TotalChildJobs,
        Succeeded = progress.Succeeded,
        Failed = progress.Failed,
        Running = progress.Running,
        Queued = progress.Queued,
        Paused = progress.Paused,
        PercentComplete = progress.PercentComplete,
        EstimatedRemaining = progress.EstimatedRemaining?.ToString(@"hh\:mm\:ss"),
        WorkloadEstimate = estimate is null ? null : BackfillWorkloadDto.From(estimate),
    };
}

public class BackfillWorkloadDto
{
    public int ScoreboardCalls { get; set; }
    public int SummaryCalls { get; set; }
    public int TotalApiCalls { get; set; }
    public string EstimatedWallClock { get; set; } = string.Empty;

    public static BackfillWorkloadDto From(BackfillWorkloadEstimate estimate) => new()
    {
        ScoreboardCalls = estimate.ScoreboardCalls,
        SummaryCalls = estimate.SummaryCalls,
        TotalApiCalls = estimate.TotalApiCalls,
        EstimatedWallClock = estimate.EstimatedWallClock.ToString(@"hh\:mm\:ss"),
    };
}

public class SqliteBackupDto
{
    public string FileName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class SqliteBackupCreatedDto
{
    public string FileName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int PrunedCount { get; set; }
}
