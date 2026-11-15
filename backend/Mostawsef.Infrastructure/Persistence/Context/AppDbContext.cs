using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Common;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Common;
using Mostawsef.Domain.Entities;
using Npgsql;

namespace Mostawsef.Infrastructure.Persistence.Context;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    public DbSet<SystemError> SystemErrors => Set<SystemError>();

    public DbSet<LabTest> LabTests => Set<LabTest>();

    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();

    public DbSet<ScheduledDump> ScheduledDumps => Set<ScheduledDump>();

    /// <summary>
    /// Stale Version → 409 concurrency_conflict. Unique/FK races (check-then-act under concurrent requests) → 409
    /// instead of a generic 500. Feature services may remap <see cref="ErrorCodes.Taken"/> to a field-specific code
    /// (e.g. username_taken).
    /// </summary>
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
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw AppException.Conflict(ErrorCodes.Taken);
        }
        catch (DbUpdateException ex) when (IsForeignKeyViolation(ex))
        {
            throw AppException.Conflict(ErrorCodes.InUse);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        FindPostgres(ex)?.SqlState == PostgresErrorCodes.UniqueViolation
        || ContainsMessage(ex, "UNIQUE constraint failed")
        || ContainsMessage(ex, "unique constraint");

    private static bool IsForeignKeyViolation(DbUpdateException ex) =>
        FindPostgres(ex)?.SqlState == PostgresErrorCodes.ForeignKeyViolation
        || ContainsMessage(ex, "FOREIGN KEY constraint failed")
        || ContainsMessage(ex, "foreign key constraint");

    private static PostgresException? FindPostgres(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException!)
        {
            if (e is PostgresException postgres)
            {
                return postgres;
            }
        }

        return null;
    }

    private static bool ContainsMessage(Exception ex, string fragment)
    {
        for (var e = ex; e is not null; e = e.InnerException!)
        {
            if (e.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
