using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Settings;

namespace Mostawsef.Infrastructure.Services;

/// <summary>Reads the single clinic-settings row once and keeps it until <see cref="Invalidate"/>.</summary>
public sealed class ClinicSettingsCache(IServiceScopeFactory scopes) : IClinicSettingsCache
{
    private readonly SingleValueCache<ClinicSettingsDto> _cache = new(
        scopes,
        (provider, ct) => provider.GetRequiredService<IClinicSettingsRepository>().GetSnapshotAsync(ct));

    public Task<ClinicSettingsDto> GetAsync(CancellationToken cancellationToken) => _cache.GetAsync(cancellationToken);

    public async Task<string> GetCurrencyCodeAsync(CancellationToken cancellationToken) =>
        (await GetAsync(cancellationToken)).CurrencyCode;

    public void Invalidate() => _cache.Invalidate();
}
