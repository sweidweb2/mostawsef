using System.Collections.Generic;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Super;

[RequiresRole(Roles.Super)]
public class SuperShellViewModel : PortalShellViewModel
{
    public SuperShellViewModel(PortalShellDependencies deps)
        : base(deps)
    {
        MenuItems =
        [
            new ShellMenuItem("IconDashboard", "Nav.Dashboard", typeof(SuperDashboardViewModel)),
            new ShellMenuItem("IconClock", "Nav.ActivityLogs", typeof(ActivityLogsViewModel)),
            new ShellMenuItem("IconWarning", "Nav.SystemErrors", typeof(SystemErrorsViewModel)),
        ];
        Start();
    }

    protected override string PortalTitleKey => "Portal.Super";

    public override IReadOnlyList<ShellMenuItem> MenuItems { get; }
}
