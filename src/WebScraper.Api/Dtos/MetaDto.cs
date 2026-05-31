namespace WebScraper.Api.Dtos;

/// <summary>
/// Data lineage envelope embedded in every response DTO. Gives consumers
/// visibility into which provider supplied the row, when it was fetched,
/// and when it was last updated in our store. Powered by the M0
/// <see cref="WebScraper.Models.IAuditableEntity"/> interface.
/// </summary>
public class MetaDto
{
    public string? Source { get; set; }
    public DateTime? FetchedAt { get; set; }
    public string? SourceRecordId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
