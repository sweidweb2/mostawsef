using Avalonia.Controls;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.ViewModels;

namespace Mostawsef.Desktop.Tests.Auth;

public class RouteTableTests
{
    private static readonly RouteTable Routes = new RouteTable()
        .Public<OpenPage, TextBlock>()
        .Portal(Roles.Admin, "Portal.Admin", portal => portal
            .Page<SharedPage, TextBlock>("IconDashboard", "Nav.Dashboard")
            .Page<AdminOnlyPage, TextBlock>("IconDashboard", "Nav.Dashboard"))
        .Portal(Roles.Super, "Portal.Super", portal => portal
            .Page<SharedPage, TextBlock>("IconDashboard", "Nav.Dashboard"));

    [Fact]
    public void A_page_in_one_portal_is_denied_to_other_roles()
    {
        Assert.True(Routes.IsAllowed(typeof(AdminOnlyPage), role => role == Roles.Admin));
        Assert.False(Routes.IsAllowed(typeof(AdminOnlyPage), role => role == Roles.Super));
    }

    [Fact]
    public void A_page_in_several_portals_is_open_to_each_of_their_roles_only()
    {
        Assert.True(Routes.IsAllowed(typeof(SharedPage), role => role == Roles.Admin));
        Assert.True(Routes.IsAllowed(typeof(SharedPage), role => role == Roles.Super));
        Assert.False(Routes.IsAllowed(typeof(SharedPage), _ => false));
    }

    [Fact]
    public void Public_pages_are_open_to_everyone_and_unlisted_pages_to_no_one()
    {
        Assert.True(Routes.IsAllowed(typeof(OpenPage), _ => false));
        Assert.False(Routes.IsAllowed(typeof(UnlistedPage), _ => true));
    }

    private sealed class OpenPage : ViewModelBase;

    private sealed class SharedPage : ViewModelBase;

    private sealed class AdminOnlyPage : ViewModelBase;

    private sealed class UnlistedPage : ViewModelBase;
}
