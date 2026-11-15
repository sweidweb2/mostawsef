using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Services.Auth;

namespace Mostawsef.Api.IntegrationTests.Auth;

public class UserAuthCacheTests
{
    private static readonly UserAuthStatus Active = new(true, UserRole.Admin, 1);

    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    [Fact]
    public void Generation_does_not_store_ids_until_invalidate()
    {
        var cache = new UserAuthCache(TimeProvider.System);
        var id = Guid.NewGuid();

        Assert.Equal(0, cache.Generation(id));
        Assert.True(cache.TrySet(id, 0, Active));
        Assert.True(cache.TryGet(id, out var status));
        Assert.Equal(Active, status);

        cache.Invalidate(id);
        Assert.Equal(1, cache.Generation(id));
        Assert.False(cache.TryGet(id, out _));
        Assert.False(cache.TrySet(id, 0, Active));
        Assert.True(cache.TrySet(id, 1, Active));
    }

    [Fact]
    public void Entries_expire_so_changes_made_outside_this_api_are_seen()
    {
        var time = new ManualTime(DateTimeOffset.UtcNow);
        var cache = new UserAuthCache(time);
        var id = Guid.NewGuid();
        Assert.True(cache.TrySet(id, 0, Active));

        time.Advance(UserAuthCache.MaxAge - TimeSpan.FromSeconds(1));
        Assert.True(cache.TryGet(id, out _));

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.False(cache.TryGet(id, out _));
    }

    [Fact]
    public void Entry_overflow_evicts_the_oldest_entries_but_keeps_the_newest_and_all_generations()
    {
        var time = new ManualTime(DateTimeOffset.UtcNow);
        var cache = new UserAuthCache(time);
        var kept = Guid.NewGuid();
        cache.Invalidate(kept);
        Assert.True(cache.TrySet(kept, 1, Active));

        var ids = new List<Guid>();
        for (var i = 0; i < UserAuthCache.MaxEntries + 1; i++)
        {
            time.Advance(TimeSpan.FromMilliseconds(10));
            ids.Add(Guid.NewGuid());
            Assert.True(cache.TrySet(ids[^1], 0, Active));
        }

        Assert.False(cache.TryGet(kept, out _)); // the oldest go first
        Assert.False(cache.TryGet(ids[0], out _));
        Assert.True(cache.TryGet(ids[^1], out _));
        Assert.True(cache.TryGet(ids[^2], out _));
        Assert.Equal(1, cache.Generation(kept)); // never forgotten: a stale read cannot be stored after Invalidate
        Assert.False(cache.TrySet(kept, 0, Active));
    }
}
