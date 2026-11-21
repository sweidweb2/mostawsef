using System.Net;
using Microsoft.AspNetCore.Hosting;
using Mostawsef.Api.IntegrationTests.Infrastructure;

namespace Mostawsef.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class RateLimitTests(ApiFactory factory)
{
    [Fact]
    public async Task Too_many_login_attempts_return_429()
    {
        using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimit:LoginPermitLimit", "3"));
        var client = limited.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var allowed = await client.LoginAsync(TestUsers.Super.Username, "wrong-password");
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var blocked = await client.LoginAsync(TestUsers.Super.Username, TestUsers.Super.Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }
}
