using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Common;

namespace Mostawsef.Infrastructure.Persistence;

/// <summary>
/// Fills the audit fields of every <see cref="Entity"/> on save, bumps its Version on update,
/// and turns the deletion of an <see cref="ISoftDeletable"/> into an update that marks it deleted.
/// </summary>
public sealed class AuditInterceptor(ICurrentUser currentUser, TimeProvider time) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        var now = time.GetUtcNow().UtcDateTime;
        var user = Text.Truncate(currentUser.Username, Entity.AuditUserMaxLength);

        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(e => e.CreatedAt).CurrentValue = now;
                entry.Property(e => e.CreatedBy).CurrentValue = user;
                continue;
            }

            if (entry.State == EntityState.Deleted && entry.Entity is SoftDeletableEntity soft)
            {
                entry.State = EntityState.Modified;
                var softEntry = context.Entry(soft);
                softEntry.Property(e => e.IsDeleted).CurrentValue = true;
                softEntry.Property(e => e.DeletedAt).CurrentValue = now;
                softEntry.Property(e => e.DeletedBy).CurrentValue = user;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Property(e => e.UpdatedAt).CurrentValue = now;
                entry.Property(e => e.UpdatedBy).CurrentValue = user;
                var version = entry.Property(e => e.Version);
                version.CurrentValue = version.OriginalValue + 1;
            }
        }
    }
}
