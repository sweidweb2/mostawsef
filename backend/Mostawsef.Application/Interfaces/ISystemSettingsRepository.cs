using Mostawsef.Contracts.Settings;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Interfaces;

public interface ISystemSettingsRepository
{
    /// <summary>Read-only snapshot (no tracking) for the cache and HTTP GET.</summary>
    Task<SystemSettingsDto> GetSnapshotAsync(CancellationToken cancellationToken);

    /// <summary>The single settings row (tracked) for updates. HTTP GET uses <see cref="ISystemSettingsCache"/>.</summary>
    Task<SystemSettings> GetForUpdateAsync(CancellationToken cancellationToken);

    /// <summary>Throws AppException (409 concurrency_conflict) if the settings changed since they were loaded.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
