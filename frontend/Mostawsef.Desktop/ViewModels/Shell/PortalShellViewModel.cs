using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Ui;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// Shared layout for every role portal (sidebar menu, top bar with user, theme, language, sign out).
/// Each role derives from it, declares [RequiresRole] and its own menu. View: Views/Shell/PortalShellView.
/// </summary>
public abstract partial class PortalShellViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly NavigationService _navigation;
    private readonly SessionService _session;
    private readonly ThemeService _theme;
    private readonly DialogService _dialogs;

    protected PortalShellViewModel(
        IServiceProvider services,
        NavigationService navigation,
        SessionService session,
        ThemeService theme,
        DialogService dialogs)
    {
        _services = services;
        _navigation = navigation;
        _session = session;
        _theme = theme;
        _dialogs = dialogs;
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

    public string UserName => _session.CurrentUser?.Username ?? string.Empty;

    public string RoleName => LocalizationService.Instance.Get($"Role.{_session.CurrentUser?.Role}");

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
        if (!_navigation.CanAccess(pageType))
        {
            _session.SignOut(SignOutReason.Expired);
            return;
        }

        CurrentPage = (ViewModelBase)_services.GetRequiredService(pageType);
    }

    [RelayCommand]
    private void ToggleTheme() => _theme.Toggle();

    [RelayCommand]
    private void ToggleLanguage() => LocalizationService.Instance.Toggle();

    [RelayCommand]
    private async Task SignOutAsync()
    {
        var loc = LocalizationService.Instance;
        if (await _dialogs.ConfirmAsync(loc.Get("Auth.LogoutConfirm"), confirmText: loc.Get("Auth.Logout")))
        {
            _session.SignOut();
        }
    }
}
