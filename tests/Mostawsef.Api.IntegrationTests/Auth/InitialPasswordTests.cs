using System.Net;
using System.Net.Http.Json;
using Mostawsef.Api.IntegrationTests.Infrastructure;
using Mostawsef.Contracts.Auth;

namespace Mostawsef.Api.IntegrationTests.Auth;

/// <summary>
/// Seeded Super accounts ship with a known initial password (appsettings.json): until the user sets their own,
/// the token may only change the password or sign out.
/// </summary>
[Collection(ApiCollection.Name)]
public class InitialPasswordTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_seeded_super_must_change_the_initial_password_before_anything_else()
    {
        var first = await factory.CreateClient().LoginAsAsync(TestUsers.InitialSuper);
        Assert.True(first.MustChangePassword);

        var limited = factory.CreateClient().WithBearer(first.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await limited.GetAsync("api/users", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await limited.GetAsync("api/system-settings", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await limited.PostAsJsonAsync("api/system/reset-data", new { }, Ct)).StatusCode);

        var changed = await limited.PostAsJsonAsync(
            "api/auth/change-password",
            new ChangePasswordRequest { CurrentPassword = TestUsers.InitialSuper.Password, NewPassword = "Own#Choice9" },
            Ct);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var renewed = (await changed.Content.ReadFromJsonAsync<LoginResponse>(Ct))!;
        Assert.False(renewed.MustChangePassword);

        Assert.Equal(
            HttpStatusCode.OK,
            (await factory.CreateClient().WithBearer(renewed.AccessToken).GetAsync("api/users", Ct)).StatusCode);
        // The limited token died with the password change.
        Assert.Equal(HttpStatusCode.Unauthorized, (await limited.GetAsync("api/users", Ct)).StatusCode);

        var again = await factory.CreateClient().LoginAsync(TestUsers.InitialSuper.Username, "Own#Choice9");
        Assert.False((await again.Content.ReadFromJsonAsync<LoginResponse>(Ct))!.MustChangePassword);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await factory.CreateClient().LoginAsync(TestUsers.InitialSuper.Username, TestUsers.InitialSuper.Password)).StatusCode);
    }

    [Fact]
    public async Task Supers_created_in_the_app_and_test_seeds_are_not_asked_to_change()
    {
        var seeded = await factory.CreateClient().LoginAsAsync(TestUsers.Super);
        var (_, created) = await (await factory.CreateClientAsAsync(TestUsers.Super)).CreateUserAsync();
        var createdSession = await factory.CreateClient().LoginAsAsync(created);

        Assert.False(seeded.MustChangePassword);
        Assert.False(createdSession.MustChangePassword);
    }
}
