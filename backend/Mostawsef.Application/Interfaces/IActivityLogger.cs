namespace Mostawsef.Application.Interfaces;

/// <summary>
/// One activity to record. User, IP and user agent are filled from the current request when not set.
/// Details is any object; it is stored as JSON.
/// </summary>
public record ActivityEntry(string Action, string Module, string Description)
{
    public Guid? UserId { get; init; }

    public string? Username { get; init; }

    public string? EntityType { get; init; }

    public string? EntityId { get; init; }

    public object? Details { get; init; }
}

/// <summary>
/// Activity log engine. Call it from Application services after every main action:
/// <code>await activity.LogAsync(new ActivityEntry(ActivityActions.PatientCreated, ActivityModules.Patients, "Created patient") { EntityType = "Patient", EntityId = id.ToString() });</code>
/// Never throws: a logging failure must not break the action being logged.
/// </summary>
public interface IActivityLogger
{
    /// <summary>Buffers the entry for this request/scope. Call <see cref="FlushAsync"/> (or let the API middleware) to write.</summary>
    Task LogAsync(ActivityEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Writes every buffered entry in one insert. Safe to call when the buffer is empty.</summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
