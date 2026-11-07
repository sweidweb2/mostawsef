namespace Mostawsef.Domain.Common;

/// <summary>
/// Base of business entities. CreatedAt/By, UpdatedAt/By and Version are set by the persistence layer
/// on every save; Version is the optimistic concurrency token (a stale one fails the save).
/// </summary>
public abstract class Entity
{
    public const int AuditUserMaxLength = 50;

    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>Username of the creator; null for system actions (e.g. seeding).</summary>
    public string? CreatedBy { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public string? UpdatedBy { get; private set; }

    /// <summary>Starts at 0 and goes up by one on every update.</summary>
    public int Version { get; private set; }
}
