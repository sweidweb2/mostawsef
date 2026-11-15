using System.Collections.Concurrent;
using Mostawsef.Application.Interfaces;

namespace Mostawsef.Infrastructure.Services.Auth;

public sealed class UserAuthCache(TimeProvider time) : IUserAuthCache
{
    /// <summary>Soft cap for a local clinic install; evicts the oldest half rather than growing forever if ids churn.</summary>
    public const int MaxEntries = 256;

    /// <summary>
    /// Safety net only: this API clears an entry on every user change it makes, so its own edits apply at once.
    /// A change made outside this process (a second API on the same database, a restore, a manual SQL fix) is seen
    /// once the entry is this old. Costs at most one small query per signed-in user per minute.
    /// </summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();
    private readonly ConcurrentDictionary<Guid, int> _generations = new();
    private readonly object _gate = new();

    public bool TryGet(Guid userId, out UserAuthStatus status)
    {
        if (_entries.TryGetValue(userId, out var entry) && time.GetUtcNow() - entry.LoadedAt < MaxAge)
        {
            status = entry.Status;
            return true;
        }

        status = default;
        return false;
    }

    public int Generation(Guid userId) => _generations.TryGetValue(userId, out var generation) ? generation : 0;

    public bool TrySet(Guid userId, int generation, UserAuthStatus status)
    {
        lock (_gate)
        {
            if ((_generations.TryGetValue(userId, out var current) ? current : 0) != generation)
            {
                return false;
            }

            var now = time.GetUtcNow();
            _entries[userId] = new Entry(status, now);
            if (_entries.Count > MaxEntries)
            {
                Evict(now, keep: userId);
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

    /// <summary>
    /// Drops expired entries, then (if still over the cap) the oldest half, so a burst of ids does not empty the
    /// whole cache; the entry just stored is never dropped. Generations stay: they only grow when a user is changed,
    /// and keeping them means a read that started before an Invalidate can never be stored after it.
    /// Runs under <see cref="_gate"/>; at most 257 entries.
    /// </summary>
    private void Evict(DateTimeOffset now, Guid keep)
    {
        foreach (var (id, entry) in _entries)
        {
            if (now - entry.LoadedAt >= MaxAge)
            {
                _entries.TryRemove(id, out _);
            }
        }

        if (_entries.Count <= MaxEntries)
        {
            return;
        }

        foreach (var (id, _) in _entries.Where(e => e.Key != keep).OrderBy(e => e.Value.LoadedAt).Take(_entries.Count / 2).ToList())
        {
            _entries.TryRemove(id, out _);
        }
    }

    private readonly record struct Entry(UserAuthStatus Status, DateTimeOffset LoadedAt);
}
