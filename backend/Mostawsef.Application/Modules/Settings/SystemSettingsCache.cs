using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Settings;

namespace Mostawsef.Application.Modules.Settings;

/// <summary>Singleton store for the system-settings row; owned by <see cref="SystemSettingsService"/>.</summary>
public sealed class SystemSettingsCache(IServiceScopeFactory scopes)
{
    private readonly SingleValueCache<SystemSettingsDto> _cache = new(
        scopes,
        (provider, ct) => provider.GetRequiredService<ISystemSettingsRepository>().GetSnapshotAsync(ct));

    public Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken) => _cache.GetAsync(cancellationToken);

    public void Invalidate() => _cache.Invalidate();
}
