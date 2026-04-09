namespace WebScraper.Models;

/// <summary>
/// Marks an entity as soft-deletable. The EF Core global query filter in AppDbContext
/// automatically hides soft-deleted rows from normal queries; use IgnoreQueryFilters()
/// in admin contexts to view them.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>True if the row has been soft-deleted.</summary>
    bool IsDeleted { get; set; }

    /// <summary>UTC timestamp when the row was soft-deleted.</summary>
    DateTime? DeletedAt { get; set; }

    /// <summary>Identifier of the user or system actor that performed the delete.</summary>
    string? DeletedBy { get; set; }

    /// <summary>Optional human-readable reason for the delete.</summary>
    string? DeleteReason { get; set; }
}
