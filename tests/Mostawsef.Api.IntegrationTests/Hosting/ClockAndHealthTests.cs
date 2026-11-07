using System.Globalization;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
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
    public async Task Audit_times_come_from_the_injected_clock()
    {
        var realNow = DateTimeOffset.UtcNow;
        var fixedNow = new DateTimeOffset(realNow.Year, realNow.Month, realNow.Day, realNow.Hour, realNow.Minute, realNow.Second, TimeSpan.Zero)
            .AddHours(-3);
        using var frozen = Frozen(fixedNow);
        var client = frozen.CreateClient();
        // Token lifetimes are validated against the real clock, so sign in with it.
        var login = await factory.CreateClient().LoginAsAsync(TestUsers.Super);
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
    }

    [Theory]
    [InlineData("2026-03-10T08:30:00+02:00", "2026-03-10T22:00:00Z")]
    [InlineData("2026-03-10T00:10:00+02:00", "2026-03-10T22:00:00Z")]
    [InlineData("2026-03-10T23:50:00+02:00", "2026-03-11T22:00:00Z")]
    public async Task Sessions_expire_at_the_next_local_midnight(string signInAt, string expectedExpiry)
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test+2", TimeSpan.FromHours(2), "Test+2", "Test+2");
        using var frozen = Frozen(DateTimeOffset.Parse(signInAt, CultureInfo.InvariantCulture), zone);

        var login = await frozen.CreateClient().LoginAsAsync(TestUsers.Admin);

        Assert.Equal(DateTimeOffset.Parse(expectedExpiry, CultureInfo.InvariantCulture).UtcDateTime, login.ExpiresAt.ToUniversalTime());
    }

    private WebApplicationFactory<Program> Frozen(DateTimeOffset now, TimeZoneInfo? zone = null) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(now, zone ?? TimeZoneInfo.Utc))));

    private sealed class FixedTimeProvider(DateTimeOffset now, TimeZoneInfo zone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => zone;
    }
}
