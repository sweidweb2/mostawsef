using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Super;

[RequiresRole(Roles.Super)]
public class SuperDashboardViewModel(SessionService session) : DashboardViewModelBase(session)
{
}
