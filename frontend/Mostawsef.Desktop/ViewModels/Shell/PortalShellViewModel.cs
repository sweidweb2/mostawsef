using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Ui;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>Everything a portal shell needs, injected as one object so role shells stay one-liners.</summary>
public record PortalShellDependencies(
    IServiceProvider Services,
    NavigationService Navigation,
    SessionService Session,
    ThemeService Theme,
    IDialogService Dialogs,
    AuthApi AuthApi);

/// <summary>
/// Shared layout for every role portal (sidebar menu, top bar with user, theme, language, sign out).
/// Each role derives from it, declares [RequiresRole] and its own menu. View: Views/Shell/PortalShellView.
/// </summary>
public abstract partial class PortalShellViewModel : ViewModelBase
{
    private readonly PortalShellDependencies _deps;

    protected PortalShellViewModel(PortalShellDependencies deps)
    {
        _deps = deps;
        LocalizationService.Instance.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PortalTitle));
            OnPropertyChanged(nameof(RoleName));
        };
    }

    /// <summary>Resource key of the portal title, e.g. "Portal.Admin".</summary>
    protected abstract string PortalTitleKey { get; }

    public abstract IReadOnlyList<ShellMenuItem> MenuItems { get; }

    public string PortalTitle => LocalizationService.Instance.Get(PortalTitleKey);

    public string UserName => _deps.Session.CurrentUser?.Username ?? string.Empty;

    public string RoleName => LocalizationService.Instance.Get($"Role.{_deps.Session.CurrentUser?.Role}");

    [ObservableProperty]
    public partial int SelectedMenuIndex { get; set; } = -1;

    [ObservableProperty]
    public partial ViewModelBase? CurrentPage { get; private set; }

    /// <summary>Called by subclasses at the end of their constructor to open the first page.</summary>
    protected void Start() => SelectedMenuIndex = 0;

    partial void OnSelectedMenuIndexChanged(int value)
    {
        if (value < 0 || value >= MenuItems.Count)
        {
            return;
        }

        var pageType = MenuItems[value].PageType;
        if (!_deps.Navigation.CanAccess(pageType))
        {
            _deps.Session.SignOut(SignOutReason.Expired);
            return;
        }

        CurrentPage = (ViewModelBase)_deps.Services.GetRequiredService(pageType);
        if (CurrentPage is ILoadablePage loadable)
        {
            _ = loadable.InitializeAsync();
        }
    }

    [RelayCommand]
    private void ToggleTheme() => _deps.Theme.Toggle();

    [RelayCommand]
    private void ToggleLanguage() => LocalizationService.Instance.Toggle();

    [RelayCommand]
    private async Task SignOutAsync()
    {
        var loc = LocalizationService.Instance;
        if (await _deps.Dialogs.ConfirmAsync(loc.Get("Auth.LogoutConfirm"), confirmText: loc.Get("Auth.Logout")))
        {
            await _deps.AuthApi.LogoutAsync();
            _deps.Session.SignOut();
        }
    }
}
