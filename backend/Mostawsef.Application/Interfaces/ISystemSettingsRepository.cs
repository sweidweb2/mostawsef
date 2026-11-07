using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Interfaces;

public interface ISystemSettingsRepository
{
    /// <summary>The single settings row (tracked), created at startup.</summary>
    Task<SystemSettings> GetAsync(CancellationToken cancellationToken);

    /// <summary>Throws AppException (409 concurrency_conflict) if the settings changed since they were loaded.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
