using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Local;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>Common bits of the role dashboards (welcome line). Each role has its own dashboard view.</summary>
public abstract class DashboardViewModelBase : ViewModelBase
{
    private readonly SessionService _session;

    protected DashboardViewModelBase(SessionService session)
    {
        _session = session;
        LocalizationService.Instance.LanguageChanged += (_, _) => OnPropertyChanged(nameof(Welcome));
    }

    public string Welcome => LocalizationService.Instance.Format("Dashboard.Welcome", _session.CurrentUser?.Username);
}
