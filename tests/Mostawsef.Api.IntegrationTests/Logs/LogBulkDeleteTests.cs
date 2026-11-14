using System.Net;
using System.Net.Http.Json;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;

namespace Mostawsef.Api.IntegrationTests.Logs;

[Collection(ApiCollection.Name)]
public class LogBulkDeleteTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Bulk_delete_removes_only_the_selected_errors_and_logs_the_deletion()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var marker = HttpClientExtensions.Unique("bulk");
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            await ReportAsync($"{marker}-{i}");
        }

        var errors = (await super.GetJsonAsync<CursorPage<SystemErrorListItemDto>>($"api/system-errors?search={marker}")).Items;
        Assert.Equal(3, errors.Count);
        ids.AddRange(errors.Take(2).Select(e => e.Id));

        var response = await super.PostAsJsonAsync("api/system-errors/bulk-delete", new DeleteLogsRequest { Ids = [.. ids, Guid.NewGuid()] }, Ct);
        response.EnsureSuccessStatusCode();

        Assert.Equal(2, (await response.Content.ReadFromJsonAsync<DeleteLogsResultDto>(Ct))!.Deleted);
        var left = (await super.GetJsonAsync<CursorPage<SystemErrorListItemDto>>($"api/system-errors?search={marker}")).Items;
        Assert.Equal(errors[2].Id, Assert.Single(left).Id);

        var logged = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=logs.errors_deleted");
        Assert.Contains(logged.Items, l => l.Action == "logs.errors_deleted" && l.Description == "Deleted 2 system error records");
    }

    [Fact]
    public async Task Bulk_delete_of_activity_logs_removes_the_selected_records()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var (_, login) = await super.CreateUserAsync();
        Assert.True((await factory.CreateClient().LoginAsync(login.Username, login.Password)).IsSuccessStatusCode);
        var marker = login.Username.Split('@')[0].Split('-')[1];
        var page = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"api/activity-logs?search={marker}");
        var created = Assert.Single(page.Items, l => l.Action == "auth.signed_in");

        var response = await super.PostAsJsonAsync("api/activity-logs/bulk-delete", new DeleteLogsRequest { Ids = [created.Id] }, Ct);
        response.EnsureSuccessStatusCode();

        Assert.Equal(1, (await response.Content.ReadFromJsonAsync<DeleteLogsResultDto>(Ct))!.Deleted);
        Assert.Equal(HttpStatusCode.NotFound, (await super.GetAsync($"api/activity-logs/{created.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Bulk_delete_without_ids_or_with_too_many_is_rejected()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        var empty = await super.PostAsJsonAsync("api/activity-logs/bulk-delete", new DeleteLogsRequest { Ids = [] }, Ct);
        var tooMany = await super.PostAsJsonAsync(
            "api/system-errors/bulk-delete",
            new DeleteLogsRequest { Ids = Enumerable.Range(0, LogRules.MaxBulkDelete + 1).Select(_ => Guid.NewGuid()).ToList() },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
    }

    [Fact]
    public async Task Delete_all_empties_the_error_log_and_records_who_did_it()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        await ReportAsync(HttpClientExtensions.Unique("clear"));

        var response = await super.DeleteAsync("api/system-errors", Ct);
        response.EnsureSuccessStatusCode();

        Assert.True((await response.Content.ReadFromJsonAsync<DeleteLogsResultDto>(Ct))!.Deleted >= 1);
        Assert.Equal(0, (await super.GetJsonAsync<SystemErrorStatsDto>("api/system-errors/stats")).Total);
        var logged = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=logs.errors_cleared");
        Assert.Contains(logged.Items, l => l.Action == "logs.errors_cleared" && l.Username == TestUsers.Super.Username);
    }

    [Fact]
    public async Task Admins_cannot_bulk_delete_or_clear_logs()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var bulk = await admin.PostAsJsonAsync("api/activity-logs/bulk-delete", new DeleteLogsRequest { Ids = [Guid.NewGuid()] }, Ct);
        var clear = await admin.DeleteAsync("api/activity-logs", Ct);
        var clearErrors = await admin.DeleteAsync("api/system-errors", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, bulk.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, clear.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, clearErrors.StatusCode);
    }

    private async Task ReportAsync(string message)
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "api/system-errors/report", new ReportClientErrorRequest(message, null, null, "Error", null, null, null), Ct);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
