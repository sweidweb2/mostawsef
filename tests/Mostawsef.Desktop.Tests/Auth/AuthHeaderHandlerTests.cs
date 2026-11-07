using System.Net;
using Avalonia.Headless.XUnit;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Tests.Infrastructure;

namespace Mostawsef.Desktop.Tests.Auth;

public class AuthHeaderHandlerTests
{
    [AvaloniaFact]
    public async Task The_token_is_added_per_request_and_never_to_the_shared_client_headers()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);

        await app.Get<AuthApi>().MeAsync(TestContext.Current.CancellationToken);

        Assert.Equal("test-token", Assert.Single(app.Api.Requests).Headers.Authorization?.Parameter);
        Assert.Null(app.Get<HttpClient>().DefaultRequestHeaders.Authorization);
    }

    [AvaloniaFact]
    public async Task Requests_after_sign_out_carry_no_token()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Session.SignOut();

        await app.Get<AuthApi>().MeAsync(TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(app.Api.Requests).Headers.Authorization);
    }

    [AvaloniaFact]
    public async Task A_late_401_for_an_earlier_session_does_not_sign_out_the_new_one()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        var newSession = new LoginResponse("new-token", DateTime.UtcNow.AddHours(8), new UserDto(Guid.NewGuid(), "other@test.local", Roles.Super));
        app.Api.Respond = _ =>
        {
            // The user signs in again while the old request is still on its way back.
            app.Session.SignIn(newSession);
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        };

        await app.Get<AuthApi>().MeAsync(TestContext.Current.CancellationToken);

        Assert.True(app.Session.IsInRole(Roles.Super));
        Assert.Equal("new-token", app.Session.AccessToken);
        Assert.Equal(SignOutReason.None, app.Session.LastSignOutReason);
    }
}
