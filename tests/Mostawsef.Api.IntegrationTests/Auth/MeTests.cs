using System.Net;
using System.Net.Http.Json;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Auth;

namespace Mostawsef.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class MeTests(ApiFactory factory)
{
    [Fact]
    public async Task Without_a_token_returns_401()
    {
        var response = await factory.CreateClient().GetAsync("api/auth/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task With_a_valid_token_returns_the_current_user()
    {
        var client = factory.CreateClient();
        var login = await client.LoginAsAsync(TestUsers.Super);

        var me = await client.WithBearer(login.AccessToken)
            .GetFromJsonAsync<UserDto>("api/auth/me", TestContext.Current.CancellationToken);

        Assert.NotNull(me);
        Assert.Equal(TestUsers.Super.Username, me.Username);
        Assert.Equal(Roles.Super, me.Role);
    }

    [Fact]
    public async Task With_a_tampered_token_returns_401()
    {
        var client = factory.CreateClient();
        var login = await client.LoginAsAsync(TestUsers.Admin);
        var tampered = login.AccessToken[..^4] + "abcd";

        var response = await client.WithBearer(tampered).GetAsync("api/auth/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task With_a_garbage_token_returns_401()
    {
        var response = await factory.CreateClient().WithBearer("not.a.jwt")
            .GetAsync("api/auth/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
