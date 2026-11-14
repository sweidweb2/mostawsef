using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Settings;

namespace Mostawsef.Infrastructure.Services;

/// <summary>Reads the single system-settings row once and keeps it until <see cref="Invalidate"/>.</summary>
public sealed class SystemSettingsCache(IServiceScopeFactory scopes) : ISystemSettingsCache
{
    private readonly SingleValueCache<SystemSettingsDto> _cache = new(
        scopes,
        (provider, ct) => provider.GetRequiredService<ISystemSettingsRepository>().GetSnapshotAsync(ct));

    public Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken) => _cache.GetAsync(cancellationToken);

    public void Invalidate() => _cache.Invalidate();
}
