using System;
using System.Collections.Generic;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

[RequiresRole(Roles.Admin)]
public class AdminShellViewModel : PortalShellViewModel
{
    public AdminShellViewModel(
        IServiceProvider services,
        NavigationService navigation,
        SessionService session,
        ThemeService theme,
        DialogService dialogs)
        : base(services, navigation, session, theme, dialogs)
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
