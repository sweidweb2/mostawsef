using System.Net;
using System.Net.Http.Json;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;

namespace Mostawsef.Api.IntegrationTests.Logs;

[Collection(ApiCollection.Name)]
public class SystemErrorTests(ApiFactory factory)
{
    [Fact]
    public async Task Unhandled_exception_is_recorded_and_the_client_gets_a_generic_500()
    {
        var message = HttpClientExtensions.Unique("boom");

        var response = await factory.CreateClient().GetAsync($"{ApiFactory.ThrowPath}?message={message}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain(message, body);
        Assert.Contains("traceId", body);

        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var item = (await super.GetJsonAsync<CursorPage<SystemErrorListItemDto>>($"api/system-errors?search={message}")).Items.Single();
        Assert.Equal("Api", item.Source);
        Assert.Equal("Error", item.Level);
        Assert.Equal(500, item.StatusCode);
        Assert.Equal(ApiFactory.ThrowPath, item.RequestPath);

        var details = await super.GetJsonAsync<SystemErrorDto>($"api/system-errors/{item.Id}");
        Assert.Equal("System.InvalidOperationException", details.ExceptionType);
        Assert.Contains("InvalidOperationException", details.StackTrace);
        Assert.Equal("GET", details.RequestMethod);
        Assert.False(string.IsNullOrEmpty(details.TraceId));
    }

    [Fact]
    public async Task Desktop_can_report_errors_without_signing_in()
    {
        var message = HttpClientExtensions.Unique("desktop-crash");

        var response = await factory.CreateClient().PostAsJsonAsync(
            "api/system-errors/report",
            new ReportClientErrorRequest(message, "System.NullReferenceException", "at Some.Method()", "Critical", "LoginView", "1.0.0", "Windows 11"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var item = (await super.GetJsonAsync<CursorPage<SystemErrorListItemDto>>($"api/system-errors?search={message}")).Items.Single();
        Assert.Equal("Desktop", item.Source);
        Assert.Equal("Critical", item.Level);

        var details = await super.GetJsonAsync<SystemErrorDto>($"api/system-errors/{item.Id}");
        Assert.Contains("1.0.0", details.Details);
        Assert.Contains("LoginView", details.Details);
        Assert.Null(details.RequestPath); // the report call itself is not where the error happened
        Assert.Null(details.RequestMethod);
    }

    [Fact]
    public async Task Report_without_a_message_returns_400()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "api/system-errors/report",
            new ReportClientErrorRequest("", null, null, null, null, null, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Report_with_a_whitespace_message_returns_400()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "api/system-errors/report",
            new ReportClientErrorRequest("   ", null, null, null, null, null, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Oversized_values_are_cut_to_the_column_limits()
    {
        var marker = HttpClientExtensions.Unique("long");
        var longMessage = marker + new string('x', 10_000);

        await factory.CreateClient().PostAsJsonAsync(
            "api/system-errors/report",
            new ReportClientErrorRequest(longMessage, null, new string('s', 50_000), null, null, null, null),
            TestContext.Current.CancellationToken);

        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var item = (await super.GetJsonAsync<CursorPage<SystemErrorListItemDto>>($"api/system-errors?search={marker}")).Items.Single();
        var details = await super.GetJsonAsync<SystemErrorDto>($"api/system-errors/{item.Id}");
        Assert.Equal(2000, details.Message.Length);
        Assert.Equal(20000, details.StackTrace!.Length);
    }

    [Fact]
    public async Task The_list_does_not_read_stack_traces_or_details_from_the_database()
    {
        var message = HttpClientExtensions.Unique("list-columns");
        await factory.CreateClient().PostAsJsonAsync(
            "api/system-errors/report",
            new ReportClientErrorRequest(message, "System.Exception", new string('s', 20_000), "Error", "Context", "1.0.0", "Windows 11"),
            TestContext.Current.CancellationToken);
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        factory.Sql.Clear();
        var item = (await super.GetJsonAsync<CursorPage<SystemErrorListItemDto>>($"api/system-errors?search={message}")).Items.Single();

        Assert.Equal(message, item.Message);
        var listQueries = factory.Sql.Queries
            .Where(q => q.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) && q.Contains("system_errors"))
            .ToList();
        Assert.NotEmpty(listQueries);
        Assert.All(listQueries, q =>
        {
            Assert.DoesNotContain("\"StackTrace\"", q);
            Assert.DoesNotContain("\"Details\"", q);
        });
    }

    [Fact]
    public async Task Delete_removes_the_error_and_records_the_deletion()
    {
        var message = HttpClientExtensions.Unique("delete-me");
        await factory.CreateClient().GetAsync($"{ApiFactory.ThrowPath}?message={message}", TestContext.Current.CancellationToken);
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var item = (await super.GetJsonAsync<CursorPage<SystemErrorListItemDto>>($"api/system-errors?search={message}")).Items.Single();

        var deleted = await super.DeleteAsync($"api/system-errors/{item.Id}", TestContext.Current.CancellationToken);
        var getAfter = await super.GetAsync($"api/system-errors/{item.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getAfter.StatusCode);
        var audit = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=logs.error_deleted");
        Assert.NotEmpty(audit.Items);

        var auditDetails = await super.GetJsonAsync<ActivityLogDto>($"api/activity-logs/{audit.Items[0].Id}");
        Assert.Equal(item.Id.ToString(), auditDetails.EntityId);
        Assert.Contains("\"source\":\"Api\"", auditDetails.Details);
    }

    [Fact]
    public async Task Deleting_an_unknown_error_returns_404()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var response = await super.DeleteAsync($"api/system-errors/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Stats_are_read_in_one_query()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        factory.Sql.Clear();

        await super.GetJsonAsync<SystemErrorStatsDto>("api/system-errors/stats");

        Assert.Single(factory.Sql.Queries, q => q.Contains("FROM system_errors") && !q.Contains("INSERT"));
    }

    [Fact]
    public async Task Stats_count_errors_and_critical_ones()
    {
        await factory.CreateClient().PostAsJsonAsync(
            "api/system-errors/report",
            new ReportClientErrorRequest(HttpClientExtensions.Unique("critical"), null, null, "Critical", null, null, null),
            TestContext.Current.CancellationToken);
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var stats = await super.GetJsonAsync<SystemErrorStatsDto>("api/system-errors/stats");

        Assert.True(stats.Total >= 1);
        Assert.True(stats.Today >= 1);
        Assert.True(stats.Last7Days >= stats.Today);
        Assert.True(stats.Critical >= 1);
    }
}
