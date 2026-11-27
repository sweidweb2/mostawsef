using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Common;

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
