using System.Collections.Concurrent;
using Mostawsef.Application.Interfaces;

namespace Mostawsef.Infrastructure.Services.Auth;

public sealed class UserAuthCache : IUserAuthCache
{
    /// <summary>Soft cap for a local clinic install; clears rather than growing forever if ids churn.</summary>
    public const int MaxEntries = 256;

    private readonly ConcurrentDictionary<Guid, UserAuthStatus> _entries = new();
    private readonly ConcurrentDictionary<Guid, int> _generations = new();
    private readonly object _gate = new();

    public bool TryGet(Guid userId, out UserAuthStatus status) => _entries.TryGetValue(userId, out status);

    public int Generation(Guid userId) => _generations.TryGetValue(userId, out var generation) ? generation : 0;

    public bool TrySet(Guid userId, int generation, UserAuthStatus status)
    {
        lock (_gate)
        {
            if ((_generations.TryGetValue(userId, out var current) ? current : 0) != generation)
            {
                return false;
            }

            _entries[userId] = status;
            if (_entries.Count > MaxEntries)
            {
                _entries.Clear();
                _generations.Clear();
                if (generation != 0)
                {
                    _generations[userId] = generation;
                }

                _entries[userId] = status;
            }

            return true;
        }
    }

    public void Invalidate(Guid userId)
    {
        lock (_gate)
        {
            _generations.AddOrUpdate(userId, 1, static (_, g) => g + 1);
            _entries.TryRemove(userId, out _);
        }
    }
}
