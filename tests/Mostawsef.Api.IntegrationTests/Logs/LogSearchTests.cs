using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;
using Mostawsef.Infrastructure.Persistence;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Api.IntegrationTests.Logs;

[Collection(ApiCollection.Name)]
public class LogSearchTests(ApiFactory factory)
{
    [Theory]
    [InlineData("api/activity-logs", "ab")]
    [InlineData("api/activity-logs", "a b")]
    [InlineData("api/activity-logs", "%_!")]
    [InlineData("api/system-errors", "ab")]
    [InlineData("api/system-errors", "ـمـ")]
    public async Task Search_with_fewer_than_3_letters_or_digits_returns_400_min_length(string path, string search)
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var response = await super.GetAsync($"{path}?search={Uri.EscapeDataString(search)}", TestContext.Current.CancellationToken);

        var problem = await ProblemResponse.ReadAsync(response);
        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal(["min_length|3"], problem.Errors["Search"]);
    }

    [Theory]
    [InlineData("api/activity-logs")]
    [InlineData("api/system-errors")]
    public async Task Blank_search_lists_without_filtering(string path)
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var response = await super.GetAsync($"{path}?search=%20%20", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Search_finds_matching_activity()
    {
        var marker = NewMarker();
        await factory.CreateClient().LoginAsync($"{marker}@test.local", "wrong");
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var page = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"api/activity-logs?search={marker}");

        var log = Assert.Single(page.Items);
        Assert.Equal($"{marker}@test.local", log.Username);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Searching_without_a_token_returns_401_before_validating_the_term()
    {
        var response = await factory.CreateClient().GetAsync("api/activity-logs?search=ab", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string NewMarker() => $"zq{Guid.NewGuid():N}"[..14];
}
