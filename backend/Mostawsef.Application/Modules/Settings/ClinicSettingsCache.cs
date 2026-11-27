using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Settings;

namespace Mostawsef.Application.Modules.Settings;

/// <summary>Singleton store for the clinic-settings row; owned by <see cref="ClinicSettingsService"/>.</summary>
public sealed class ClinicSettingsCache(IServiceScopeFactory scopes)
{
    private readonly SingleValueCache<ClinicSettingsDto> _cache = new(
        scopes,
        (provider, ct) => provider.GetRequiredService<IClinicSettingsRepository>().GetSnapshotAsync(ct));

    public Task<ClinicSettingsDto> GetAsync(CancellationToken cancellationToken) => _cache.GetAsync(cancellationToken);

    public async Task<string> GetCurrencyCodeAsync(CancellationToken cancellationToken) =>
        (await GetAsync(cancellationToken)).CurrencyCode;

    public void Invalidate() => _cache.Invalidate();
}
