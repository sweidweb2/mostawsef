using System.Net;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;

namespace Mostawsef.Api.IntegrationTests.Logs;

[Collection(ApiCollection.Name)]
public class ActivityLogTests(ApiFactory factory)
{
    [Fact]
    public async Task Successful_sign_in_is_recorded_with_user_and_ip()
    {
        await factory.CreateClientAsAsync(TestUsers.Admin);
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var page = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>(
            $"api/activity-logs?search={TestUsers.Admin.Username}&pageSize=100");

        var signIn = Assert.Single(page.Items.Where(i => i.Action == "auth.signed_in").Take(1));
        Assert.Equal(TestUsers.Admin.Username, signIn.Username);
        Assert.Equal("Auth", signIn.Module);

        var details = await super.GetJsonAsync<ActivityLogDto>($"api/activity-logs/{signIn.Id}");
        Assert.NotNull(details.UserId);
        Assert.Contains("Admin", details.Details);
    }

    [Fact]
    public async Task Failed_sign_in_is_recorded_with_the_attempted_username()
    {
        var attempted = $"intruder-{Guid.NewGuid():N}"[..21] + "@test.local";
        await factory.CreateClient().LoginAsync(attempted, "wrong");
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var page = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"api/activity-logs?search={attempted}");

        var failed = Assert.Single(page.Items);
        Assert.Equal("auth.sign_in_failed", failed.Action);
        Assert.Equal(attempted, failed.Username);
    }

    [Fact]
    public async Task Sign_out_is_recorded()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var response = await admin.PostAsync("api/auth/logout", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var page = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=auth.signed_out");
        Assert.Contains(page.Items, i => i.Username == TestUsers.Admin.Username);
    }

    [Fact]
    public async Task Next_cursors_walk_every_row_newest_first_without_gaps_or_repeats()
    {
        var (super, list, newestFirst) = await SeedFailedSignInsAsync(5);

        var page1 = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"{list}&pageSize=2");
        var page2 = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"{list}&pageSize=2&after={page1.NextCursor}");
        var page3 = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"{list}&pageSize=2&after={page2.NextCursor}");

        Assert.Equal(newestFirst, page1.Items.Concat(page2.Items).Concat(page3.Items).Select(i => i.Id));
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(2, page1.PageSize);
        Assert.Null(page1.PreviousCursor);
        Assert.NotNull(page2.PreviousCursor);
        Assert.Null(page3.NextCursor);
    }

    [Fact]
    public async Task Previous_cursors_walk_back_to_the_same_pages()
    {
        var (super, list, _) = await SeedFailedSignInsAsync(5);
        var page1 = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"{list}&pageSize=2");
        var page2 = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"{list}&pageSize=2&after={page1.NextCursor}");
        var page3 = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"{list}&pageSize=2&after={page2.NextCursor}");

        var back2 = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"{list}&pageSize=2&before={page3.PreviousCursor}");
        var back1 = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"{list}&pageSize=2&before={back2.PreviousCursor}");

        Assert.Equal(page2.Items.Select(i => i.Id), back2.Items.Select(i => i.Id));
        Assert.Equal(page1.Items.Select(i => i.Id), back1.Items.Select(i => i.Id));
        Assert.Null(back1.PreviousCursor);
    }

    [Fact]
    public async Task Last_returns_the_oldest_rows_newest_first()
    {
        var (super, list, newestFirst) = await SeedFailedSignInsAsync(5);

        var last = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"{list}&pageSize=2&last=true");

        Assert.Equal(newestFirst[^2..], last.Items.Select(i => i.Id));
        Assert.Null(last.NextCursor);
        Assert.NotNull(last.PreviousCursor);
    }

    [Fact]
    public async Task Going_back_past_the_newest_rows_returns_the_full_first_page()
    {
        var (super, list, newestFirst) = await SeedFailedSignInsAsync(5);
        var last = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"{list}&pageSize=2&last=true");

        // Only 3 rows are newer than the last page: a page of 4 cannot end there, so the first 4 rows come back.
        var back = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"{list}&pageSize=4&before={last.PreviousCursor}");

        Assert.Equal(newestFirst[..4], back.Items.Select(i => i.Id));
        Assert.Null(back.PreviousCursor);
        Assert.NotNull(back.NextCursor);
    }

    [Fact]
    public async Task Page_moves_skip_the_count_and_never_use_offset()
    {
        var (super, list, _) = await SeedFailedSignInsAsync(3);
        var page1 = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"{list}&pageSize=1");
        factory.Sql.Clear();

        var page2 = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"{list}&pageSize=1&after={page1.NextCursor}&includeTotal=false");

        Assert.Null(page2.TotalCount);
        Assert.Single(page2.Items);
        var listQueries = factory.Sql.Queries.Where(q => q.Contains("FROM activity_logs") && !q.Contains("INSERT")).ToList();
        Assert.Single(listQueries);
        Assert.DoesNotContain("COUNT", listQueries[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OFFSET", listQueries[0], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("after=not-a-cursor")]
    [InlineData("before=123")]
    [InlineData("after=638000000000000000-0123456789abcdef0123456789abcdef&last=true")]
    [InlineData("after=638000000000000000-0123456789abcdef0123456789abcdef&before=638000000000000000-0123456789abcdef0123456789abcdef")]
    public async Task Invalid_or_conflicting_cursors_return_400(string paging)
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var response = await super.GetAsync($"api/activity-logs?{paging}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Page_size_is_capped()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var page = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?pageSize=100000");

        Assert.Equal(CursorQuery.MaxPageSize, page.PageSize);
    }

    [Fact]
    public async Task Stats_are_read_in_one_query()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        factory.Sql.Clear();

        await super.GetJsonAsync<ActivityLogStatsDto>("api/activity-logs/stats");

        Assert.Single(factory.Sql.Queries, q => q.Contains("FROM activity_logs") && !q.Contains("INSERT"));
    }

    [Fact]
    public async Task Stats_count_todays_activity_and_failed_sign_ins()
    {
        await factory.CreateClient().LoginAsync(TestUsers.Admin.Username, "wrong");
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var stats = await super.GetJsonAsync<ActivityLogStatsDto>("api/activity-logs/stats");

        Assert.True(stats.Total >= 2);
        Assert.True(stats.Today >= 2);
        Assert.True(stats.Last7Days >= stats.Today);
        Assert.True(stats.FailedSignInsToday >= 1);
    }

    [Fact]
    public async Task Delete_removes_the_record_and_records_the_deletion()
    {
        var attempted = HttpClientExtensions.Unique("to-delete");
        await factory.CreateClient().LoginAsync(attempted, "x");
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var target = (await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"api/activity-logs?search={attempted}")).Items.Single();

        var deleted = await super.DeleteAsync($"api/activity-logs/{target.Id}", TestContext.Current.CancellationToken);
        var getAfter = await super.GetAsync($"api/activity-logs/{target.Id}", TestContext.Current.CancellationToken);
        var deleteAgain = await super.DeleteAsync($"api/activity-logs/{target.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getAfter.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteAgain.StatusCode);

        var audit = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=logs.activity_deleted&pageSize=100");
        Assert.Contains(audit.Items, i => i.Username == TestUsers.Super.Username);

        var auditDetails = await super.GetJsonAsync<ActivityLogDto>($"api/activity-logs/{audit.Items[0].Id}");
        Assert.Equal(target.Id.ToString(), auditDetails.EntityId);
        Assert.Null(auditDetails.Details);
    }

    [Fact]
    public async Task Unknown_record_returns_404()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var response = await super.GetAsync($"api/activity-logs/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Records <paramref name="count"/> failed sign-ins only this test's search matches.
    /// Returns the list URL filtered to them and their ids, newest first.
    /// </summary>
    private async Task<(HttpClient Super, string List, List<Guid> NewestFirst)> SeedFailedSignInsAsync(int count)
    {
        var marker = HttpClientExtensions.Unique("paging");
        for (var i = 0; i < count; i++)
        {
            await factory.CreateClient().LoginAsync($"{marker}-{i}", "wrong");
        }

        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var list = $"api/activity-logs?search={marker}";
        var all = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"{list}&pageSize=100");
        Assert.Equal(count, all.Items.Count);
        return (super, list, all.Items.Select(i => i.Id).ToList());
    }
}
