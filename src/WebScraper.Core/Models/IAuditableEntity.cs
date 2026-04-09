namespace WebScraper.Models;

/// <summary>
/// Marks an entity as tracking data lineage (which provider produced the row and when)
/// plus creation/update timestamps. All fields except CreatedAt/UpdatedAt are nullable so
/// manually seeded or admin-edited rows don't require a data source.
/// </summary>
public interface IAuditableEntity
{
    /// <summary>
    /// Name of the data provider that produced this row (e.g. "Espn", "SportsDataIo").
    /// Null for manually created/edited rows.
    /// </summary>
    string? DataSource { get; set; }

    /// <summary>
    /// UTC timestamp of the scrape that produced this row's current state.
    /// </summary>
    DateTime? DataSourceFetchedAt { get; set; }

    /// <summary>
    /// The provider's native identifier for this record (e.g. ESPN athlete id).
    /// Helps debugging and future re-crawls.
    /// </summary>
    string? DataSourceRecordId { get; set; }

    /// <summary>UTC timestamp when the row was first inserted.</summary>
    DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the most recent update.</summary>
    DateTime UpdatedAt { get; set; }
}
