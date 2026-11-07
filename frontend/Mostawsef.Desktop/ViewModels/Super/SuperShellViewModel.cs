using System;
using System.Collections.Generic;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Super;

[RequiresRole(Roles.Super)]
public class SuperShellViewModel : PortalShellViewModel
{
    public SuperShellViewModel(
        IServiceProvider services,
        NavigationService navigation,
        SessionService session,
        ThemeService theme,
        DialogService dialogs)
        : base(services, navigation, session, theme, dialogs)
    {
        MenuItems =
        [
            new ShellMenuItem("IconDashboard", "Nav.Dashboard", typeof(SuperDashboardViewModel)),
        ];
        Start();
    }

    protected override string PortalTitleKey => "Portal.Super";

    public override IReadOnlyList<ShellMenuItem> MenuItems { get; }
}
