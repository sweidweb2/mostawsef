using System.Collections.Concurrent;
using Mostawsef.Application.Interfaces;

namespace Mostawsef.Infrastructure.Services.Auth;

public sealed class UserAuthCache : IUserAuthCache
{
    /// <summary>Soft cap for a local clinic install; clears rather than growing forever if ids churn.</summary>
    public const int MaxEntries = 256;

    private readonly ConcurrentDictionary<Guid, UserAuthStatus> _entries = new();

    public bool TryGet(Guid userId, out UserAuthStatus status) => _entries.TryGetValue(userId, out status);

    public void Set(Guid userId, UserAuthStatus status)
    {
        _entries[userId] = status;
        if (_entries.Count > MaxEntries)
        {
            _entries.Clear();
            _entries[userId] = status;
        }
    }

    public void Invalidate(Guid userId) => _entries.TryRemove(userId, out _);
}
