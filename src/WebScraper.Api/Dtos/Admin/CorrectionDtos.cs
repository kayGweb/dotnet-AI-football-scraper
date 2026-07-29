using WebScraper.Models;

namespace WebScraper.Api.Dtos.Admin;

public class ProposeCorrectionRequest
{
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Field { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
}

public class DataCorrectionDto
{
    public long Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string NewValue { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string ProposedBy { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
}

public static class DataCorrectionMappings
{
    public static DataCorrectionDto ToDto(this DataCorrection c) => new()
    {
        Id = c.Id,
        EntityType = c.EntityType,
        EntityId = c.EntityId,
        Field = c.Field,
        OldValue = c.OldValue,
        NewValue = c.NewValue,
        Rationale = c.Rationale,
        ProposedBy = c.ProposedBy,
        Status = c.Status.ToString(),
        CreatedAt = c.CreatedAt,
        ResolvedAt = c.ResolvedAt,
        ResolvedBy = c.ResolvedBy,
    };
}
