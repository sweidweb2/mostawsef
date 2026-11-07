using System.Net;
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

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Super)]
    public async Task Any_signed_in_user_changes_their_own_password(string role)
    {
        var (_, login) = await (await factory.CreateClientAsAsync(TestUsers.Super)).CreateUserAsync(role);
        var client = await factory.CreateClientAsAsync(login);

        var response = await client.PostAsJsonAsync(
            "api/auth/change-password", new ChangePasswordRequest { CurrentPassword = login.Password, NewPassword = "Changed#Pass7" }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
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
