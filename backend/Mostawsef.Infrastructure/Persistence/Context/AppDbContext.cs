using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Common;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Common;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Infrastructure.Persistence.Context;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    public DbSet<SystemError> SystemErrors => Set<SystemError>();

    /// <summary>A save that hits a stale Version throws AppException (409 concurrency_conflict).</summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw AppException.Conflict(ErrorCodes.ConcurrencyConflict);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Trigram GIN indexes let PostgreSQL serve LIKE '%term%' searches. Index-only: queries stay plain LIKE,
        // so they keep working on SQLite.
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplyEntityConventions(modelBuilder);
    }

    /// <summary>Audit columns and the Version concurrency token for every Entity; a "not deleted" filter for every ISoftDeletable.</summary>
    private static void ApplyEntityConventions(ModelBuilder modelBuilder)
    {
        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(t => typeof(Entity).IsAssignableFrom(t.ClrType))
            .Select(t => t.ClrType)
            .ToList();

        foreach (var type in entityTypes)
        {
            var entity = modelBuilder.Entity(type);
            entity.Property(nameof(Entity.CreatedAt)).IsRequired();
            entity.Property(nameof(Entity.CreatedBy)).HasMaxLength(Entity.AuditUserMaxLength);
            entity.Property(nameof(Entity.UpdatedBy)).HasMaxLength(Entity.AuditUserMaxLength);
            entity.Property(nameof(Entity.Version)).IsConcurrencyToken();

            if (typeof(ISoftDeletable).IsAssignableFrom(type))
            {
                entity.Property(nameof(ISoftDeletable.DeletedBy)).HasMaxLength(Entity.AuditUserMaxLength);

                var e = Expression.Parameter(type, "e");
                entity.HasQueryFilter(Expression.Lambda(Expression.Not(Expression.Property(e, nameof(ISoftDeletable.IsDeleted))), e));
            }
        }
    }
}
