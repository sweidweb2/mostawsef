using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

[RequiresRole(Roles.Admin)]
public class AdminDashboardViewModel(SessionService session) : DashboardViewModelBase(session)
{
}
