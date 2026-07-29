namespace WebScraper.Models;

/// <summary>
/// Team identity as of a specific season (name, city, abbreviation, conference, division).
/// </summary>
public class TeamSeason : IAuditableEntity, ISoftDeletable
{
    public int Id { get; set; }
    public int FranchiseId { get; set; }
    public int Season { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Conference { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;

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

    public Franchise Franchise { get; set; } = null!;
    public ICollection<Game> HomeGames { get; set; } = new List<Game>();
    public ICollection<Game> AwayGames { get; set; } = new List<Game>();
    public ICollection<TeamGameStats> TeamStats { get; set; } = new List<TeamGameStats>();
    public ICollection<PlayerTeamSeason> PlayerRosterEntries { get; set; } = new List<PlayerTeamSeason>();
}
