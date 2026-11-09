using Mostawsef.Contracts.Settings;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Interfaces;

public interface ISystemSettingsRepository
{
    /// <summary>Read-only snapshot (no tracking) for GET and login maintenance checks.</summary>
    Task<SystemSettingsDto> GetSnapshotAsync(CancellationToken cancellationToken);

    /// <summary>The single settings row (tracked), for updates. Created at startup.</summary>
    Task<SystemSettings> GetForUpdateAsync(CancellationToken cancellationToken);

    /// <summary>Throws AppException (409 concurrency_conflict) if the settings changed since they were loaded.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
