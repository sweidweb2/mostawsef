using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.ViewModels.Admin;
using Mostawsef.Desktop.ViewModels.Auth;
using Mostawsef.Desktop.ViewModels.Dev;
using Mostawsef.Desktop.ViewModels.Shell;
using Mostawsef.Desktop.ViewModels.Super;
using Mostawsef.Desktop.Views.Admin;
using Mostawsef.Desktop.Views.Auth;
using Mostawsef.Desktop.Views.Dev;
using Mostawsef.Desktop.Views.Shell;
using Mostawsef.Desktop.Views.Super;

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
        .View<ActivityLogDetailsViewModel, ActivityLogDetailsView>()
        .View<SystemErrorDetailsViewModel, SystemErrorDetailsView>()
        .View<ChangePasswordFormViewModel, ChangePasswordFormView>()
        .View<UserFormViewModel, UserFormView>()
        .View<ResetPasswordFormViewModel, ResetPasswordFormView>()
        .View<ClinicSettingsFormViewModel, ClinicSettingsFormView>()
        .View<SystemSettingsFormViewModel, SystemSettingsFormView>()
        .View<LabTestFormViewModel, LabTestFormView>()
        .View<PatientFormViewModel, PatientFormView>()
        .View<AddPaymentFormViewModel, AddPaymentFormView>()
        .Portal(Roles.Admin, "Portal.Admin", portal => portal
            .Page<DashboardViewModel, DashboardView>("IconDashboard", "Nav.Dashboard")
            .Page<PatientsViewModel, PatientsView>("IconUser", "Nav.Patients")
            .SubPage<PatientDetailsViewModel, PatientDetailsView>()
            .Page<OrdersViewModel, OrdersView>("IconDocument", "Nav.Orders")
            .SubPage<NewOrderViewModel, NewOrderView>()
            .SubPage<OrderDetailsViewModel, OrderDetailsView>()
            .Page<LabTestsViewModel, LabTestsView>("IconLab", "Nav.LabTests")
            .Page<ClinicSettingsViewModel, ClinicSettingsView>("IconSettings", "Nav.ClinicSettings"))
        .Portal(Roles.Super, "Portal.Super", portal => portal
            .Page<SuperDashboardViewModel, SuperDashboardView>("IconDashboard", "Nav.Dashboard")
            .Page<UsersViewModel, UsersView>("IconUsers", "Nav.Users")
            .Page<ActivityLogsViewModel, ActivityLogsView>("IconClock", "Nav.ActivityLogs")
            .Page<SystemErrorsViewModel, SystemErrorsView>("IconWarning", "Nav.SystemErrors")
            .Page<BackupsViewModel, BackupsView>("IconDownload", "Nav.Backups")
            .Page<SystemSettingsViewModel, SystemSettingsView>("IconSettings", "Nav.SystemSettings"));
}
