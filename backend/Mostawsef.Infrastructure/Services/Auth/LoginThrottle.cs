using Microsoft.Extensions.Configuration;
using Mostawsef.Application.Interfaces;

namespace Mostawsef.Infrastructure.Services.Auth;

/// <summary>
/// In-memory failed-sign-in counter per username (one API process). Configuration:
/// <c>RateLimit:LoginFailuresPerUser</c> (default 10) failures within <c>RateLimit:LoginFailureWindowMinutes</c> (default 5).
/// </summary>
public sealed class LoginThrottle(TimeProvider time, IConfiguration configuration) : ILoginThrottle
{
    /// <summary>Bound on tracked usernames; a flood of distinct names drops the tracking instead of growing without limit.</summary>
    public const int MaxTrackedUsernames = 10_000;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, (int Failures, DateTime WindowStart)> _entries = new(StringComparer.Ordinal);
    private readonly int _limit = Math.Max(1, configuration.GetValue("RateLimit:LoginFailuresPerUser", 10));
    private readonly TimeSpan _window = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("RateLimit:LoginFailureWindowMinutes", 5)));

    public bool IsBlocked(string username)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(username, out var entry))
            {
                return false;
            }

            if (Now() - entry.WindowStart >= _window)
            {
                _entries.Remove(username);
                return false;
            }

            return entry.Failures >= _limit;
        }
    }

    public void RecordFailure(string username)
    {
        lock (_gate)
        {
            var now = Now();
            if (_entries.TryGetValue(username, out var entry) && now - entry.WindowStart < _window)
            {
                _entries[username] = (entry.Failures + 1, entry.WindowStart);
                return;
            }

            if (_entries.Count >= MaxTrackedUsernames)
            {
                RemoveExpired(now);
                if (_entries.Count >= MaxTrackedUsernames)
                {
                    _entries.Clear();
                }
            }

            _entries[username] = (1, now);
        }
    }

    public void Reset(string username)
    {
        lock (_gate)
        {
            _entries.Remove(username);
        }
    }

    private DateTime Now() => time.GetUtcNow().UtcDateTime;

    private void RemoveExpired(DateTime now)
    {
        foreach (var name in _entries.Where(e => now - e.Value.WindowStart >= _window).Select(e => e.Key).ToList())
        {
            _entries.Remove(name);
        }
    }
}
