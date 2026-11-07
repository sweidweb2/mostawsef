using System.Collections.Generic;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

[RequiresRole(Roles.Admin)]
public class AdminShellViewModel : PortalShellViewModel
{
    public AdminShellViewModel(PortalShellDependencies deps)
        : base(deps)
    {
        MenuItems =
        [
            new ShellMenuItem("IconDashboard", "Nav.Dashboard", typeof(AdminDashboardViewModel)),
        ];
        Start();
    }

    protected override string PortalTitleKey => "Portal.Admin";

    public override IReadOnlyList<ShellMenuItem> MenuItems { get; }
}
