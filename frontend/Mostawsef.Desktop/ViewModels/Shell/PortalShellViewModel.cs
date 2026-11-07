using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Ui;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// The shared layout of every role portal (sidebar menu, top bar with user, sign out), configured by a
/// <see cref="PortalDefinition"/> from AppRoutes. Created by NavigationService for the signed-in role.
/// </summary>
public partial class PortalShellViewModel : ViewModelBase
{
    private readonly NavigationService _navigation;
    private readonly SessionService _session;
    private readonly IDialogService _dialogs;
    private readonly LocalizationService _localization;
    private readonly AuthApi _authApi;

    /// <summary>Pages already opened, by menu index: switching back reuses them instead of creating and loading them again.</summary>
    private readonly ViewModelBase?[] _pages;

    public PortalShellViewModel(
        PortalDefinition portal,
        NavigationService navigation,
        SessionService session,
        IDialogService dialogs,
        LocalizationService localization,
        AuthApi authApi)
    {
        _navigation = navigation;
        _session = session;
        _dialogs = dialogs;
        _localization = localization;
        _authApi = authApi;

        PortalTitleKey = portal.TitleKey;
        MenuItems = portal.Pages;
        _pages = new ViewModelBase?[MenuItems.Count];
        SelectedMenuIndex = 0;
    }

    /// <summary>Resource key of the portal title, e.g. "Portal.Admin".</summary>
    public string PortalTitleKey { get; }

    public IReadOnlyList<PortalPage> MenuItems { get; }

    public string UserName => _session.CurrentUser?.Username ?? string.Empty;

    /// <summary>Resource key of the user's role name, e.g. "Role.Super".</summary>
    public string RoleKey => $"Role.{_session.CurrentUser?.Role}";

    [ObservableProperty]
    public partial int SelectedMenuIndex { get; set; } = -1;

    [ObservableProperty]
    public partial ViewModelBase? CurrentPage { get; private set; }

    partial void OnSelectedMenuIndexChanged(int value)
    {
        if (value < 0 || value >= MenuItems.Count)
        {
            return;
        }

        var pageType = MenuItems[value].PageType;
        var page = _navigation.CanAccess(pageType) ? _pages[value] ??= _navigation.CreatePage(pageType) : null;
        if (page is null)
        {
            _session.SignOut(SignOutReason.Expired);
            return;
        }

        (CurrentPage as ILoadablePage)?.Deactivate();
        CurrentPage = page;
        if (page is ILoadablePage loadable)
        {
            _ = loadable.ActivateAsync();
        }
    }

    /// <summary>Called when the portal is replaced (e.g. sign-out): cancels the open page's API calls.</summary>
    public void Close() => (CurrentPage as ILoadablePage)?.Deactivate();

    [RelayCommand]
    private async Task SignOutAsync()
    {
        if (await _dialogs.ConfirmAsync(_localization.Get("Auth.LogoutConfirm"), confirmText: _localization.Get("Auth.Logout")))
        {
            await _authApi.LogoutAsync();
            _session.SignOut();
        }
    }
}
