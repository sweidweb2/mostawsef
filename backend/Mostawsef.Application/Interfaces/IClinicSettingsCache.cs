using Mostawsef.Contracts.Settings;

namespace Mostawsef.Application.Interfaces;

/// <summary>The single clinic-settings row, cached until invalidated after an edit.</summary>
public interface IClinicSettingsCache
{
    Task<ClinicSettingsDto> GetAsync(CancellationToken cancellationToken);

    Task<string> GetCurrencyCodeAsync(CancellationToken cancellationToken);

    void Invalidate();
}
