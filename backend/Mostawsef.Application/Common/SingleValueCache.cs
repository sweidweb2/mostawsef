using Microsoft.Extensions.DependencyInjection;

namespace Mostawsef.Application.Common;

/// <summary>
/// Loads one value on first use, shares it with concurrent callers (one load at a time) and keeps it until
/// <see cref="Invalidate"/>. A load that raced an invalidation is discarded and repeated, so a stale value is never kept.
/// The loader runs in its own DI scope because the cache is a singleton.
/// </summary>
public sealed class SingleValueCache<T>(IServiceScopeFactory scopes, Func<IServiceProvider, CancellationToken, Task<T>> load)
    where T : class
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile T? _cached;
    private int _generation;

    public async Task<T> GetAsync(CancellationToken cancellationToken)
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
                var value = await load(scope.ServiceProvider, cancellationToken);

                if (generation != Volatile.Read(ref _generation))
                {
                    continue; // Invalidate ran while we were reading: load again under the same gate.
                }

                _cached = value;

                // Invalidate can still win between the check and the assign: drop the stale value and reload.
                if (generation != Volatile.Read(ref _generation))
                {
                    if (ReferenceEquals(_cached, value))
                    {
                        _cached = null;
                    }

                    continue;
                }

                return value;
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
