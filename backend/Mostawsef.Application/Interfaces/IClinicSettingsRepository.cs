using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Interfaces;

public interface IClinicSettingsRepository
{
    /// <summary>The single settings row (tracked) for updates. HTTP GET uses <see cref="IClinicSettingsCache"/>.</summary>
    Task<ClinicSettings> GetAsync(CancellationToken cancellationToken);

    /// <summary>Throws AppException (409 concurrency_conflict) if the settings changed since they were loaded.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
