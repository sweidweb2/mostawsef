using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Services.Auth;

namespace Mostawsef.Api.IntegrationTests.Auth;

public class UserAuthCacheTests
{
    private static readonly UserAuthStatus Active = new(true, UserRole.Admin, 1);

    [Fact]
    public void Generation_does_not_store_ids_until_invalidate()
    {
        var cache = new UserAuthCache();
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
    public void Entry_overflow_clears_generations_with_entries()
    {
        var cache = new UserAuthCache();
        Guid last = default;
        for (var i = 0; i < UserAuthCache.MaxEntries + 1; i++)
        {
            last = Guid.NewGuid();
            cache.Invalidate(last);
            Assert.True(cache.TrySet(last, 1, Active));
        }

        Assert.True(cache.TryGet(last, out _));
        Assert.Equal(1, cache.Generation(last));
        Assert.False(cache.TryGet(Guid.NewGuid(), out _));
    }
}
