using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Common;

namespace Mostawsef.Infrastructure.Persistence;

/// <summary>
/// Fills the audit fields of every <see cref="Entity"/> on save, bumps its Version on update,
/// and turns the deletion of an <see cref="ISoftDeletable"/> into an update that marks it deleted.
/// </summary>
public sealed class AuditInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
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
        var now = DateTime.UtcNow;
        var user = Text.Truncate(currentUser.Username, Entity.AuditUserMaxLength);

        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                Set(entry, nameof(Entity.CreatedAt), now);
                Set(entry, nameof(Entity.CreatedBy), user);
                continue;
            }

            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable)
            {
                entry.State = EntityState.Modified;
                Set(entry, nameof(ISoftDeletable.IsDeleted), true);
                Set(entry, nameof(ISoftDeletable.DeletedAt), now);
                Set(entry, nameof(ISoftDeletable.DeletedBy), user);
            }

            if (entry.State == EntityState.Modified)
            {
                Set(entry, nameof(Entity.UpdatedAt), now);
                Set(entry, nameof(Entity.UpdatedBy), user);
                var version = entry.Property(e => e.Version);
                version.CurrentValue = version.OriginalValue + 1;
            }
        }
    }

    private static void Set(EntityEntry<Entity> entry, string property, object? value) =>
        entry.Property(property).CurrentValue = value;
}
