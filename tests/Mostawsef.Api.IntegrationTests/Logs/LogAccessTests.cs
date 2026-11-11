using System.Net;
using Mostawsef.Api.IntegrationTests.Infrastructure;

namespace Mostawsef.Api.IntegrationTests.Logs;

[Collection(ApiCollection.Name)]
public class LogAccessTests(ApiFactory factory)
{
    public static TheoryData<string> SuperOnlyEndpoints =>
    [
        "api/activity-logs",
        "api/activity-logs/stats",
        $"api/activity-logs/{Guid.NewGuid()}",
        "api/system-errors",
        "api/system-errors/stats",
        $"api/system-errors/{Guid.NewGuid()}",
    ];

    [Theory]
    [MemberData(nameof(SuperOnlyEndpoints))]
    public async Task Signed_out_users_get_401(string url)
    {
        var response = await factory.CreateClient().GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(SuperOnlyEndpoints))]
    public async Task Admins_get_403(string url)
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var response = await admin.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admins_cannot_delete_system_errors()
    {
        var admin = await factory.CreateClientAsAsync(TestUsers.Admin);

        var response = await admin.DeleteAsync($"api/system-errors/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
