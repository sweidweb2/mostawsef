using System.Net;
using Mostawsef.Api.IntegrationTests.Infrastructure;

namespace Mostawsef.Api.IntegrationTests.Auth;

/// <summary>How the API treats the bearer token on a signed-in endpoint.</summary>
[Collection(ApiCollection.Name)]
public class TokenTests(ApiFactory factory)
{
    [Fact]
    public async Task Without_a_token_returns_401()
    {
        var response = await factory.CreateClient().GetAsync("api/clinic-settings", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_second_authenticated_call_reuses_the_cached_auth_status()
    {
        var client = await factory.CreateClientAsAsync(TestUsers.Admin);
        (await client.GetAsync("api/clinic-settings", TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        factory.Sql.Clear();
        (await client.GetAsync("api/clinic-settings", TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        Assert.DoesNotContain(
            factory.Sql.Queries,
            q => q.Contains("FROM users", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task With_a_tampered_token_returns_401()
    {
        var client = factory.CreateClient();
        var login = await client.OpenClinicSessionAsync();
        var tampered = login.AccessToken[..^4] + "abcd";

        var response = await client.WithBearer(tampered).GetAsync("api/clinic-settings", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task With_a_garbage_token_returns_401()
    {
        var response = await factory.CreateClient().WithBearer("not.a.jwt")
            .GetAsync("api/clinic-settings", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
