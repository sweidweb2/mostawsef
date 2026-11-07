using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Users;

namespace Mostawsef.Api.IntegrationTests.Hosting;

[Collection(ApiCollection.Name)]
public class ClockAndHealthTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_reports_the_api_version_the_desktop_compares_with()
    {
        var health = await factory.CreateClient().GetJsonAsync<HealthDto>("api/health");

        Assert.Equal("Healthy", health.Status);
        Assert.Equal(ApiVersion.Current, health.ApiVersion);
    }

    [Fact]
    public async Task Audit_times_and_token_expiry_come_from_the_injected_clock()
    {
        var realNow = DateTimeOffset.UtcNow;
        var fixedNow = new DateTimeOffset(realNow.Year, realNow.Month, realNow.Day, realNow.Hour, realNow.Minute, realNow.Second, TimeSpan.Zero)
            .AddHours(-3);
        using var frozen = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FixedTimeProvider(fixedNow))));
        var client = frozen.CreateClient();
        var login = await client.LoginAsAsync(TestUsers.Super);
        client.WithBearer(login.AccessToken);

        var response = await client.PostAsJsonAsync("api/users", new CreateUserRequest
        {
            FullName = "Clock Test",
            Username = $"{HttpClientExtensions.Unique("clock")}@test.local",
            Password = "Welcome#123",
        }, Ct);
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        var created = await response.Content.ReadFromJsonAsync<UserDetailsDto>(Ct);

        Assert.Equal(fixedNow.UtcDateTime, created!.CreatedAt);
        Assert.Equal(fixedNow.UtcDateTime.AddHours(8), login.ExpiresAt.ToUniversalTime(), TimeSpan.FromSeconds(1));
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
