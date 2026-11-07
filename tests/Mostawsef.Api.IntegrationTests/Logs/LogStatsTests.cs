using System.Net.Http.Json;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Logs;

namespace Mostawsef.Api.IntegrationTests.Logs;

[Collection(ApiCollection.Name)]
public class LogStatsTests(ApiFactory factory)
{
    [Fact]
    public async Task Activity_stats_count_a_new_failed_sign_in_in_every_number()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var before = await super.GetJsonAsync<ActivityLogStatsDto>("api/activity-logs/stats");

        await factory.CreateClient().LoginAsync(HttpClientExtensions.Unique("nobody"), "wrong");
        var after = await super.GetJsonAsync<ActivityLogStatsDto>("api/activity-logs/stats");

        Assert.Equal(before.Total + 1, after.Total);
        Assert.Equal(before.Today + 1, after.Today);
        Assert.Equal(before.Last7Days + 1, after.Last7Days);
        Assert.Equal(before.FailedSignInsToday + 1, after.FailedSignInsToday);
    }

    [Fact]
    public async Task Error_stats_count_a_new_critical_error_in_every_number()
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);
        var before = await super.GetJsonAsync<SystemErrorStatsDto>("api/system-errors/stats");

        var report = await factory.CreateClient().PostAsJsonAsync(
            "api/system-errors/report",
            new ReportClientErrorRequest("Stats test crash", null, null, ErrorLevels.Critical, null, null, null),
            TestContext.Current.CancellationToken);
        report.EnsureSuccessStatusCode();
        var after = await super.GetJsonAsync<SystemErrorStatsDto>("api/system-errors/stats");

        Assert.Equal(before.Total + 1, after.Total);
        Assert.Equal(before.Today + 1, after.Today);
        Assert.Equal(before.Last7Days + 1, after.Last7Days);
        Assert.Equal(before.Critical + 1, after.Critical);
    }

    [Theory]
    [InlineData("api/activity-logs/stats", "activity_logs")]
    [InlineData("api/system-errors/stats", "system_errors")]
    public async Task Stats_are_read_in_one_statement(string path, string table)
    {
        var super = await factory.CreateClientAsAsync(TestUsers.Super);

        factory.Sql.Clear();
        await super.GetJsonAsync<object>(path);

        Assert.Single(factory.Sql.Queries, q => q.Contains(table, StringComparison.Ordinal));
    }
}
