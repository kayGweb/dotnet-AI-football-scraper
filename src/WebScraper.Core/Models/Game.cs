namespace WebScraper.Models;

public class Game : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public int Season { get; set; }
    public int Week { get; set; }
    public DateTime GameDate { get; set; }
    public int HomeTeamId { get; set; }
    public int AwayTeamId { get; set; }
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }

    // Venue & metadata
    public int? VenueId { get; set; }
    public int? Attendance { get; set; }
    public bool NeutralSite { get; set; }
    public string? EspnEventId { get; set; }
    public string? GameStatus { get; set; }
    public bool? HomeWinner { get; set; }

    // Quarter scores
    public int? HomeQ1 { get; set; }
    public int? HomeQ2 { get; set; }
    public int? HomeQ3 { get; set; }
    public int? HomeQ4 { get; set; }
    public int? HomeOT { get; set; }
    public int? AwayQ1 { get; set; }
    public int? AwayQ2 { get; set; }
    public int? AwayQ3 { get; set; }
    public int? AwayQ4 { get; set; }
    public int? AwayOT { get; set; }

    // Data lineage
    public string? DataSource { get; set; }
    public DateTime? DataSourceFetchedAt { get; set; }
    public string? DataSourceRecordId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public string? DeleteReason { get; set; }

    // Navigation properties
    public Team HomeTeam { get; set; } = null!;
    public Team AwayTeam { get; set; } = null!;
    public Venue? Venue { get; set; }
    public ICollection<PlayerGameStats> PlayerStats { get; set; } = new List<PlayerGameStats>();
    public ICollection<TeamGameStats> TeamStats { get; set; } = new List<TeamGameStats>();
    public ICollection<Injury> Injuries { get; set; } = new List<Injury>();
    public ICollection<ApiLink> ApiLinks { get; set; } = new List<ApiLink>();
}
