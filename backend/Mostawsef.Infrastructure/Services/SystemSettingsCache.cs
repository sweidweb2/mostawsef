using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Settings;

namespace Mostawsef.Infrastructure.Services;

/// <summary>Reads the single system-settings row once and keeps it until <see cref="Invalidate"/>.</summary>
public sealed class SystemSettingsCache(IServiceScopeFactory scopes) : ISystemSettingsCache
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SystemSettingsDto? _cached;
    private int _generation;

    public async Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        if (_cached is { } hit)
        {
            return hit;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            while (true)
            {
                if (_cached is { } again)
                {
                    return again;
                }

                var generation = Volatile.Read(ref _generation);
                await using var scope = scopes.CreateAsyncScope();
                var dto = await scope.ServiceProvider
                    .GetRequiredService<ISystemSettingsRepository>()
                    .GetSnapshotAsync(cancellationToken);

                if (generation != Volatile.Read(ref _generation))
                {
                    // Invalidate ran while we were reading — load again under the same gate.
                    continue;
                }

                _cached = dto;

                // Invalidate can still win between the check and the assign; drop stale and reload.
                if (generation != Volatile.Read(ref _generation))
                {
                    if (ReferenceEquals(_cached, dto))
                    {
                        _cached = null;
                    }

                    continue;
                }

                return dto;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate()
    {
        Interlocked.Increment(ref _generation);
        _cached = null;
    }
}
