using Mostawsef.Contracts.Settings;

namespace Mostawsef.Application.Interfaces;

/// <summary>The single system-settings row, cached until invalidated after an edit.</summary>
public interface ISystemSettingsCache
{
    Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken);

    void Invalidate();
}
