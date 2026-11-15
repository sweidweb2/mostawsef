using System.Net;
using Avalonia.Headless.XUnit;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Auth;
using Mostawsef.Desktop.ViewModels.Shell;
using Mostawsef.Desktop.ViewModels.Super;

namespace Mostawsef.Desktop.Tests.Auth;

[Collection(UiTestCollection.Name)]
public class NavigationGuardTests : UiTestBase
{
    [AvaloniaFact]
    public void Signed_out_users_cannot_open_any_portal_or_portal_page()
    {
        using var app = new TestServices();

        Assert.False(app.Navigation.NavigateToPortal(Roles.Admin));
        Assert.IsType<LoginViewModel>(app.Navigation.CurrentPage);

        Assert.False(app.Navigation.NavigateToPortal(Roles.Super));
        Assert.False(app.Navigation.NavigateTo<DashboardViewModel>());
        Assert.Null(app.Navigation.CreatePage(typeof(DashboardViewModel)));
        Assert.IsType<LoginViewModel>(app.Navigation.CurrentPage);
    }

    [AvaloniaFact]
    public void Admin_cannot_open_the_super_portal()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);

        Assert.False(app.Navigation.NavigateToPortal(Roles.Super));
        Assert.IsType<LoginViewModel>(app.Navigation.CurrentPage);
    }

    [AvaloniaFact]
    public void Super_cannot_open_the_admin_portal()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);

        Assert.False(app.Navigation.NavigateToPortal(Roles.Admin));
    }

    [AvaloniaFact]
    public void Home_routes_each_role_to_its_own_portal_with_the_dashboard_open()
    {
        using var app = new TestServices();

        app.SignInAs(Roles.Admin);
        app.Navigation.NavigateToHome();
        var admin = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
        Assert.Equal("Portal.Admin", admin.PortalTitleKey);
        Assert.IsType<DashboardViewModel>(admin.CurrentPage);

        app.SignInAs(Roles.Super);
        app.Navigation.NavigateToHome();
        var super = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
        Assert.Equal("Portal.Super", super.PortalTitleKey);
        Assert.IsType<SuperDashboardViewModel>(super.CurrentPage);
    }

    [AvaloniaFact]
    public void Every_portal_menu_page_opens_for_its_role_and_not_for_other_roles_unless_they_list_it()
    {
        using var app = new TestServices();

        foreach (var portal in AppRoutes.Table.Portals)
        {
            app.SignInAs(portal.Role);
            Assert.NotEmpty(portal.Pages);
            Assert.All(portal.Pages, page => Assert.True(app.Navigation.CanAccess(page.PageType), $"{portal.Role} -> {page.PageType.Name}"));

            var otherPages = AppRoutes.Table.Portals
                .Where(p => p.Role != portal.Role)
                .SelectMany(p => p.Pages)
                .Where(page => portal.Pages.All(own => own.PageType != page.PageType));
            Assert.All(otherPages, page => Assert.False(app.Navigation.CanAccess(page.PageType), $"{portal.Role} -> {page.PageType.Name}"));
        }
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

        await app.Get<ClinicSettingsApi>().GetAsync(TestContext.Current.CancellationToken);

        Assert.False(app.Session.IsAuthenticated);
        Assert.Equal(SignOutReason.Expired, app.Session.LastSignOutReason);
        var login = Assert.IsType<LoginViewModel>(app.Navigation.CurrentPage);
        Assert.Equal("Login.SessionExpired", login.ErrorKey);
        Assert.StartsWith("Your session has expired", app.Localization.Get(login.ErrorKey!));
    }
}
