using Mostawsef.Contracts.Settings;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Interfaces;

public interface IClinicSettingsRepository
{
    /// <summary>Read-only snapshot (no tracking) for the cache and HTTP GET.</summary>
    Task<ClinicSettingsDto> GetSnapshotAsync(CancellationToken cancellationToken);

    /// <summary>The single settings row (tracked) for updates. HTTP GET uses <see cref="Modules.Settings.IClinicSettingsService"/>.</summary>
    Task<ClinicSettings> GetAsync(CancellationToken cancellationToken);

    /// <summary>Throws AppException (409 concurrency_conflict) if the settings changed since they were loaded.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
