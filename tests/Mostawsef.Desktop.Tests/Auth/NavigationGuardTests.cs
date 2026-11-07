using System.Net;
using Avalonia.Headless.XUnit;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Admin;
using Mostawsef.Desktop.ViewModels.Auth;
using Mostawsef.Desktop.ViewModels.Super;

namespace Mostawsef.Desktop.Tests.Auth;

public class NavigationGuardTests
{
    [AvaloniaFact]
    public void Signed_out_users_cannot_open_any_portal()
    {
        using var app = new TestServices();

        Assert.False(app.Navigation.NavigateTo<AdminShellViewModel>());
        Assert.IsType<LoginViewModel>(app.Navigation.CurrentPage);

        Assert.False(app.Navigation.NavigateTo<SuperShellViewModel>());
        Assert.IsType<LoginViewModel>(app.Navigation.CurrentPage);
    }

    [AvaloniaFact]
    public void Admin_cannot_open_the_super_portal_or_its_pages()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);

        Assert.False(app.Navigation.NavigateTo<SuperShellViewModel>());
        Assert.False(app.Navigation.CanAccess(typeof(SuperDashboardViewModel)));
        Assert.IsType<LoginViewModel>(app.Navigation.CurrentPage);
    }

    [AvaloniaFact]
    public void Super_cannot_open_the_admin_portal_or_its_pages()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);

        Assert.False(app.Navigation.NavigateTo<AdminShellViewModel>());
        Assert.False(app.Navigation.CanAccess(typeof(AdminDashboardViewModel)));
    }

    [AvaloniaFact]
    public void Home_routes_each_role_to_its_own_portal_with_the_dashboard_open()
    {
        using var app = new TestServices();

        app.SignInAs(Roles.Admin);
        app.Navigation.NavigateToHome();
        var admin = Assert.IsType<AdminShellViewModel>(app.Navigation.CurrentPage);
        Assert.IsType<AdminDashboardViewModel>(admin.CurrentPage);

        app.SignInAs(Roles.Super);
        app.Navigation.NavigateToHome();
        var super = Assert.IsType<SuperShellViewModel>(app.Navigation.CurrentPage);
        Assert.IsType<SuperDashboardViewModel>(super.CurrentPage);
    }

    [AvaloniaFact]
    public void Signing_out_returns_to_the_login_screen()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Navigation.NavigateToHome();

        app.Session.SignOut();

        Assert.IsType<LoginViewModel>(app.Navigation.CurrentPage);
        Assert.False(app.Session.IsAuthenticated);
        Assert.Equal(SignOutReason.UserRequested, app.Session.LastSignOutReason);
    }

    [AvaloniaFact]
    public async Task A_401_from_the_api_expires_the_session_and_shows_the_login_screen()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Navigation.NavigateToHome();
        app.Api.RespondWithStatus(HttpStatusCode.Unauthorized);

        await app.Get<AuthApi>().MeAsync(TestContext.Current.CancellationToken);

        Assert.False(app.Session.IsAuthenticated);
        Assert.Equal(SignOutReason.Expired, app.Session.LastSignOutReason);
        var login = Assert.IsType<LoginViewModel>(app.Navigation.CurrentPage);
        Assert.StartsWith("Your session has expired", login.ErrorMessage);
    }

    [AvaloniaFact]
    public void Signed_in_requests_carry_the_bearer_token()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);

        _ = app.Get<AuthApi>().MeAsync(TestContext.Current.CancellationToken);

        var request = Assert.Single(app.Api.Requests);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
    }
}
