using DotGlasses.Application.Common;
using DotGlasses.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DotGlasses.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Populates IAuditable fields automatically and turns a hard delete of an ISoftDeletable
/// entity into a soft delete (State flip from Deleted to Modified), per CLAUDE.md's
/// audit/soft-delete contract.
/// </summary>
public class AuditSaveChangesInterceptor(ICurrentUserContext currentUserContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditFields(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var user = currentUserContext.UserName ?? currentUserContext.UserId?.ToString() ?? "system";

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable softDeletable)
            {
                entry.State = EntityState.Modified;
                softDeletable.IsDeleted = true;
                softDeletable.DeletedAtUtc = now;
                softDeletable.DeletedBy = user;
            }

            if (entry.Entity is IAuditable auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAtUtc = now;
                    auditable.CreatedBy = user;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.ModifiedAtUtc = now;
                    auditable.ModifiedBy = user;
                }
            }
        }
    }
}
