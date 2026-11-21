using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Users;

namespace Mostawsef.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public class ChangePasswordTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_signed_in_super_changes_their_own_password()
    {
        var (_, login) = await (await factory.CreateClientAsAsync(TestUsers.Super)).CreateUserAsync();
        var oldSession = await factory.CreateClient().LoginAsAsync(login);
        var client = factory.CreateClient().WithBearer(oldSession.AccessToken);

        var response = await client.PostAsJsonAsync(
            "api/auth/change-password", new ChangePasswordRequest { CurrentPassword = login.Password, NewPassword = "Changed#Pass7" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renewed = await response.Content.ReadFromJsonAsync<LoginResponse>(Ct);
        Assert.False(string.IsNullOrWhiteSpace(renewed!.AccessToken));
        Assert.NotEqual(oldSession.AccessToken, renewed.AccessToken);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await factory.CreateClient().WithBearer(oldSession.AccessToken).GetAsync("api/clinic-settings", Ct)).StatusCode);

        var withNew = factory.CreateClient();
        withNew.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", renewed.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await withNew.GetAsync("api/clinic-settings", Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().LoginAsync(login.Username, login.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().LoginAsync(login.Username, "Changed#Pass7")).StatusCode);
    }

    [Fact]
    public async Task A_wrong_current_password_returns_400_on_that_field()
    {
        var (_, login) = await (await factory.CreateClientAsAsync(TestUsers.Super)).CreateUserAsync();
        var client = await factory.CreateClientAsAsync(login);

        var problem = await ProblemResponse.ReadAsync(await client.PostAsJsonAsync(
            "api/auth/change-password", new ChangePasswordRequest { CurrentPassword = "not-it", NewPassword = "Changed#Pass7" }, Ct));

        Assert.Equal(HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal(ErrorCodes.Validation, problem.Code);
        Assert.Equal([ErrorCodes.WrongPassword], problem.Errors["CurrentPassword"]);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().LoginAsync(login.Username, login.Password)).StatusCode);
    }

    [Fact]
    public async Task A_short_new_password_returns_400()
    {
        var (_, login) = await (await factory.CreateClientAsAsync(TestUsers.Super)).CreateUserAsync();
        var client = await factory.CreateClientAsAsync(login);

        var problem = await ProblemResponse.ReadAsync(await client.PostAsJsonAsync(
            "api/auth/change-password", new ChangePasswordRequest { CurrentPassword = login.Password, NewPassword = "short" }, Ct));

        Assert.Equal([$"min_length|{UserRules.PasswordMinLength}"], problem.Errors["NewPassword"]);
    }

    [Fact]
    public async Task Without_a_token_returns_401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "api/auth/change-password", new ChangePasswordRequest { CurrentPassword = "a", NewPassword = "Changed#Pass7" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
