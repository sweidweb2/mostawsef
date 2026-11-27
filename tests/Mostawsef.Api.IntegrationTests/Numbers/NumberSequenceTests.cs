using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Api.IntegrationTests.Numbers;

[Collection(ApiCollection.Name)]
public class NumberSequenceTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Numbers_start_at_one_and_follow_each_other_per_name()
    {
        var numbers = Sequence();
        var name = HttpClientExtensions.Unique("seq")[..30];
        var other = HttpClientExtensions.Unique("other")[..30];

        Assert.Equal(1, await numbers.NextAsync(name, Ct));
        Assert.Equal(2, await numbers.NextAsync(name, Ct));
        Assert.Equal(1, await numbers.NextAsync(other, Ct));
        Assert.Equal(3, await numbers.NextAsync(name, Ct));
    }

    [Fact]
    public async Task Parallel_callers_never_get_the_same_number()
    {
        factory.CreateClient();
        var name = HttpClientExtensions.Unique("parallel")[..30];

        // Each caller needs its own scope: the sequence shares the request DbContext.
        var taken = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var numbers = scope.ServiceProvider.GetRequiredService<INumberSequence>();
            return await numbers.NextAsync(name, Ct);
        }, Ct)));

        Assert.Equal(Enumerable.Range(1, 20).Select(i => (long)i), taken.Order());
    }

    [Fact]
    public async Task Taking_a_first_number_leaves_the_callers_unsaved_changes_alone()
    {
        factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var numbers = scope.ServiceProvider.GetRequiredService<INumberSequence>();
        var pendingName = HttpClientExtensions.Unique("pending")[..30];
        var pending = db.Add(new NumberSequence(pendingName, 7));

        Assert.Equal(1, await numbers.NextAsync(HttpClientExtensions.Unique("first")[..30], Ct));

        // The sequence shares the request DbContext: it must neither save nor drop what the caller is building.
        Assert.Equal(EntityState.Added, pending.State);
        Assert.False(await db.Set<NumberSequence>().AsNoTracking().AnyAsync(s => s.Name == pendingName, Ct));
    }

    [Fact]
    public async Task A_first_number_taken_inside_a_transaction_rolls_back_with_it()
    {
        factory.CreateClient();
        var name = HttpClientExtensions.Unique("rollback")[..30];
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(Ct);
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<INumberSequence>().NextAsync(name, Ct));
            await transaction.RollbackAsync(Ct);
        }

        Assert.Equal(1, await Sequence().NextAsync(name, Ct));
    }

    [Theory]
    [InlineData("P", 1, "P-000001")]
    [InlineData("INV", 123456, "INV-123456")]
    [InlineData("P", 1234567, "P-1234567")]
    public void Document_numbers_have_a_prefix_and_six_digits(string prefix, long value, string expected) =>
        Assert.Equal(expected, DocumentNumber.Format(prefix, value));

    private INumberSequence Sequence()
    {
        factory.CreateClient();
        return factory.Services.CreateScope().ServiceProvider.GetRequiredService<INumberSequence>();
    }
}
