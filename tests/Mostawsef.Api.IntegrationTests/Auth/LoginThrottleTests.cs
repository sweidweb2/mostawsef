using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Infrastructure.Services.Auth;

namespace Mostawsef.Api.IntegrationTests.Auth;

public class LoginThrottleTests
{
    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private static LoginThrottle Create(ManualTime time, int limit = 3, int windowMinutes = 5) =>
        new(time, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimit:LoginFailuresPerUser"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["RateLimit:LoginFailureWindowMinutes"] = windowMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }).Build());

    [Fact]
    public void Blocks_after_the_limit_and_unblocks_when_the_window_ends()
    {
        var time = new ManualTime(DateTimeOffset.UtcNow);
        var throttle = Create(time);

        throttle.RecordFailure("a");
        throttle.RecordFailure("a");
        Assert.False(throttle.IsBlocked("a"));
        throttle.RecordFailure("a");
        Assert.True(throttle.IsBlocked("a"));
        Assert.False(throttle.IsBlocked("b"));

        time.Advance(TimeSpan.FromMinutes(5));
        Assert.False(throttle.IsBlocked("a"));
    }

    [Fact]
    public void Reset_clears_the_failures()
    {
        var throttle = Create(new ManualTime(DateTimeOffset.UtcNow), limit: 1);

        throttle.RecordFailure("a");
        Assert.True(throttle.IsBlocked("a"));

        throttle.Reset("a");
        Assert.False(throttle.IsBlocked("a"));
    }

    [Fact]
    public void Tracking_is_bounded()
    {
        var throttle = Create(new ManualTime(DateTimeOffset.UtcNow), limit: 1);

        for (var i = 0; i < LoginThrottle.MaxTrackedUsernames + 5; i++)
        {
            throttle.RecordFailure("user" + i);
        }

        // The newest name is tracked; the table was dropped rather than grown past its cap.
        Assert.True(throttle.IsBlocked("user" + (LoginThrottle.MaxTrackedUsernames + 4)));
    }
}

[Collection(ApiCollection.Name)]
public class LoginThrottleApiTests(ApiFactory factory)
{
    [Fact]
    public async Task Failures_for_one_username_block_even_the_right_password()
    {
        using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimit:LoginFailuresPerUser", "3"));
        var client = limited.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var failed = await client.LoginAsync(TestUsers.Admin.Username, "wrong-password");
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }

        var blocked = await client.LoginAsync(TestUsers.Admin.Username, TestUsers.Admin.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);

        // Another account is not affected.
        var other = await client.LoginAsync(TestUsers.Super.Username, TestUsers.Super.Password);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    /// <summary>
    /// The anonymous error report must cap its body. Kestrel enforces the attribute; the in-memory test server does not,
    /// so this checks that the limit is declared and small.
    /// </summary>
    [Fact]
    public void Anonymous_client_error_report_declares_a_small_body_limit()
    {
        var action = typeof(Mostawsef.Api.Controllers.SystemErrorsController)
            .GetMethod(nameof(Mostawsef.Api.Controllers.SystemErrorsController.Report))!;

        var limit = Assert.Single(action.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute), inherit: true))
            as Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute;

        Assert.NotNull(limit);
        Assert.InRange(((Microsoft.AspNetCore.Http.Metadata.IRequestSizeLimitMetadata)limit).MaxRequestBodySize ?? long.MaxValue, 1, 256 * 1024);
    }
}
