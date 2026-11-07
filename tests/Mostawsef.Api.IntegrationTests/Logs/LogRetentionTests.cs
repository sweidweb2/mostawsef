using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Logs;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Api.IntegrationTests.Logs;

[Collection(ApiCollection.Name)]
public class LogRetentionTests(ApiFactory factory)
{
    [Fact]
    public async Task Records_past_the_retention_period_are_purged_and_newer_ones_kept()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var oldActivity = await CreateActivityAsync(super);
        var recentActivity = await CreateActivityAsync(super);
        var oldError = await CreateErrorAsync(super);
        var recentError = await CreateErrorAsync(super);
        await AgeAsync(oldActivity, oldError, TimeSpan.FromDays(366), TimeSpan.FromDays(91));
        await AgeAsync(recentActivity, recentError, TimeSpan.FromDays(364), TimeSpan.FromDays(89));

        var result = await PurgeAsync(scope => scope.ServiceProvider.GetRequiredService<ILogRetentionService>());

        Assert.True(result.ActivityLogs >= 1);
        Assert.True(result.SystemErrors >= 1);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(super, $"api/activity-logs/{oldActivity}"));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(super, $"api/system-errors/{oldError}"));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(super, $"api/activity-logs/{recentActivity}"));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(super, $"api/system-errors/{recentError}"));
    }

    [Fact]
    public async Task A_purge_that_removed_records_is_recorded_as_an_activity()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var old = await CreateActivityAsync(super);
        await AgeAsync(old, null, TimeSpan.FromDays(400), TimeSpan.Zero);

        await PurgeAsync(scope => scope.ServiceProvider.GetRequiredService<ILogRetentionService>());

        var audit = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>("api/activity-logs?search=logs.purged");
        Assert.Contains(audit.Items, i => i.Action == "logs.purged" && i.Module == "Logs");
    }

    [Fact]
    public async Task Zero_days_keeps_records_forever()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var activity = await CreateActivityAsync(super);
        var error = await CreateErrorAsync(super);
        await AgeAsync(activity, error, TimeSpan.FromDays(3650), TimeSpan.FromDays(3650));

        var result = await PurgeAsync(scope => new LogRetentionService(
            scope.ServiceProvider.GetRequiredService<IActivityLogRepository>(),
            scope.ServiceProvider.GetRequiredService<ISystemErrorRepository>(),
            scope.ServiceProvider.GetRequiredService<IActivityLogger>(),
            Options.Create(new LogRetentionOptions { ActivityLogDays = 0, SystemErrorDays = 0 }),
            TimeProvider.System));

        Assert.Equal(new LogPurgeResult(0, 0), result);
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(super, $"api/activity-logs/{activity}"));
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(super, $"api/system-errors/{error}"));
    }

    [Fact]
    public void Api_does_not_start_with_a_negative_retention_period()
    {
        using var badFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["LogRetention:ActivityLogDays"] = "-1" })));

        var error = Record.Exception(() => badFactory.CreateClient());

        Assert.NotNull(error);
        Assert.True(error.ToString().Contains(nameof(LogRetentionOptions.ActivityLogDays)), error.ToString());
    }

    private async Task<Guid> CreateActivityAsync(HttpClient super)
    {
        var attempted = HttpClientExtensions.Unique("retain");
        await factory.CreateClient().LoginAsync(attempted, "wrong");

        var page = await super.GetJsonAsync<CursorPage<ActivityLogListItemDto>>($"api/activity-logs?search={attempted}");
        return Assert.Single(page.Items).Id;
    }

    private async Task<Guid> CreateErrorAsync(HttpClient super)
    {
        var message = HttpClientExtensions.Unique("retain");
        var response = await factory.CreateClient().PostAsJsonAsync(
            "api/system-errors/report",
            new ReportClientErrorRequest(message, null, null, null, null, null, null),
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var page = await super.GetJsonAsync<CursorPage<SystemErrorListItemDto>>($"api/system-errors?search={message}");
        return Assert.Single(page.Items).Id;
    }

    /// <summary>Moves records back in time, as if they had been written that long ago.</summary>
    private async Task AgeAsync(Guid? activityId, Guid? errorId, TimeSpan activityAge, TimeSpan errorAge)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        await db.ActivityLogs.Where(l => l.Id == activityId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.OccurredAt, now - activityAge), cancellationToken);
        await db.SystemErrors.Where(e => e.Id == errorId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.OccurredAt, now - errorAge), cancellationToken);
    }

    private async Task<LogPurgeResult> PurgeAsync(Func<AsyncServiceScope, ILogRetentionService> resolve)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await resolve(scope).PurgeExpiredAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<HttpStatusCode> StatusAsync(HttpClient client, string url) =>
        (await client.GetAsync(url, TestContext.Current.CancellationToken)).StatusCode;
}
