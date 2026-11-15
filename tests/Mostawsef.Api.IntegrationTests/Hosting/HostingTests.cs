using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mostawsef.Api.IntegrationTests.Infrastructure;

namespace Mostawsef.Api.IntegrationTests.Hosting;

[Collection(ApiCollection.Name)]
public class HostingTests(ApiFactory factory)
{
    [Fact]
    public async Task Http_requests_are_not_redirected_to_https_when_an_https_port_is_configured()
    {
        var token = (await factory.CreateClient().LoginAsAsync(TestUsers.Admin)).AccessToken;
        using var httpsFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["HTTPS_PORT"] = "7027" })));
        var client = httpsFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.WithBearer(token).GetAsync("api/clinic-settings", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Ef_core_sql_commands_are_not_logged_by_default()
    {
        factory.CreateClient();
        var logger = factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Microsoft.EntityFrameworkCore.Database.Command");

        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
    }

    [Fact]
    public async Task Api_responses_include_server_timing_except_health()
    {
        var client = factory.CreateClient();
        var token = (await client.LoginAsAsync(TestUsers.Admin)).AccessToken;

        var me = await client.WithBearer(token).GetAsync("api/clinic-settings", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.True(me.Headers.TryGetValues("Server-Timing", out var timing));
        Assert.Contains(timing, v => v.Contains("app;dur=", StringComparison.Ordinal));

        var health = await client.GetAsync("api/health", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.False(health.Headers.Contains("Server-Timing"));
    }
}
