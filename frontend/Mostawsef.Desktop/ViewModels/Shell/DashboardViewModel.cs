using Mostawsef.Desktop.Services.Auth;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// Home page of every portal (welcome line). Shared by all roles until one needs its own dashboard;
/// then give that role a separate view model + view in AppRoutes.
/// </summary>
public class DashboardViewModel(SessionService session) : ViewModelBase
{
    public string UserName => session.CurrentUser?.Username ?? string.Empty;
}
