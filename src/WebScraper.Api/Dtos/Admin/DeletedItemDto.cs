namespace WebScraper.Api.Dtos.Admin;

/// <summary>
/// Generic envelope for any soft-deleted row across the eight domain entities. Admins
/// see a unified list and can restore one at a time. We deliberately don't return the
/// full original row — the EntityType + Id + a one-line summary is enough for review,
/// and the data is still queryable via the per-entity endpoints with IgnoreQueryFilters.
/// </summary>
public class DeletedItemDto
{
    /// <summary>Entity type name (e.g. "Team", "Player", "Game").</summary>
    public string EntityType { get; set; } = string.Empty;

    public int Id { get; set; }

    /// <summary>Best-effort human label (team name, player name, "Game {season} W{week}", etc.).</summary>
    public string Label { get; set; } = string.Empty;

    public DateTime DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public string? DeleteReason { get; set; }
}
