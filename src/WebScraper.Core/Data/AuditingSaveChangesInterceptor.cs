using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WebScraper.Models;

namespace WebScraper.Data;

/// <summary>
/// EF Core interceptor that stamps auditing fields (CreatedAt/UpdatedAt) and converts
/// physical deletes into soft deletes for any entity implementing the relevant marker
/// interfaces. Registered via DbContextOptionsBuilder.AddInterceptors(...) in the
/// ServiceCollectionExtensions wiring.
///
/// Rules:
/// - Added IAuditableEntity rows get CreatedAt/UpdatedAt stamped to UtcNow.
/// - Modified IAuditableEntity rows get UpdatedAt stamped to UtcNow (CreatedAt untouched).
/// - Deleted ISoftDeletable rows are converted to Modified with IsDeleted=true, DeletedAt stamped.
///
/// Scrapers remain responsible for setting DataSource / DataSourceFetchedAt / DataSourceRecordId
/// since those are provider-specific lineage data the interceptor can't infer.
/// </summary>
public class AuditingSaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditing(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditing(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ApplyAuditing(DbContext? context)
    {
        if (context is null) return;

        var now = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is ISoftDeletable softDeletable && entry.State == EntityState.Deleted)
            {
                // Convert hard delete into soft delete
                entry.State = EntityState.Modified;
                softDeletable.IsDeleted = true;
                softDeletable.DeletedAt = now;
            }

            if (entry.Entity is IAuditableEntity auditable)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        if (auditable.CreatedAt == default)
                            auditable.CreatedAt = now;
                        auditable.UpdatedAt = now;
                        break;
                    case EntityState.Modified:
                        auditable.UpdatedAt = now;
                        // Don't overwrite CreatedAt on updates
                        entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
                        break;
                }
            }
        }
    }
}
