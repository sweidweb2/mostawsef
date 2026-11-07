using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.ViewModels.Auth;
using Mostawsef.Desktop.ViewModels.Dev;
using Mostawsef.Desktop.ViewModels.Shell;
using Mostawsef.Desktop.Views.Auth;
using Mostawsef.Desktop.Views.Dev;
using Mostawsef.Desktop.Views.Shell;

namespace Mostawsef.Desktop;

/// <summary>
/// All screens in one place. DI registration, NavigationService (access checks), the portal shell (menus)
/// and ViewLocator (views) read from this table. A page listed in several portals is open to each of their roles.
/// </summary>
public static class AppRoutes
{
    public static RouteTable Table { get; } = new RouteTable()
        .Public<LoginViewModel, LoginView>()
        .Public<GalleryViewModel, GalleryView>()
        .View<PortalShellViewModel, PortalShellView>()
        .Portal(Roles.Admin, "Portal.Admin", portal => portal
            .Page<DashboardViewModel, DashboardView>("IconDashboard", "Nav.Dashboard"))
        .Portal(Roles.Super, "Portal.Super", portal => portal
            .Page<DashboardViewModel, DashboardView>("IconDashboard", "Nav.Dashboard"));
}
