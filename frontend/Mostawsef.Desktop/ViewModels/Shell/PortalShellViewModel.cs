using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Auth;

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
    private readonly IToastService _toasts;
    private readonly PerformanceMonitor _perf;

    /// <summary>Set while the menu goes back to its entry because the user kept a sub-page's changes.</summary>
    private bool _restoringMenu;

    /// <summary>The menu page under an open sub-page; kept so Back restores it without recreating (or reloading unless stale).</summary>
    private ViewModelBase? _menuPage;

    /// <summary>Menu pages kept for the portal lifetime so returning to a page reuses its view model (reload only when stale).</summary>
    private readonly Dictionary<int, ViewModelBase> _menuPages = [];

    public PortalShellViewModel(
        PortalDefinition portal,
        NavigationService navigation,
        SessionService session,
        IDialogService dialogs,
        LocalizationService localization,
        AuthApi authApi,
        IToastService toasts,
        ConnectionMonitor connection,
        ClinicContext clinic,
        PerformanceMonitor perf)
    {
        _navigation = navigation;
        _session = session;
        _dialogs = dialogs;
        _localization = localization;
        _authApi = authApi;
        _toasts = toasts;
        _perf = perf;
        Connection = connection;

        PortalTitleKey = portal.TitleKey;
        MenuItems = portal.Pages;

        connection.Start();
        _ = clinic.LoadAsync();
        SelectedMenuIndex = 0;
    }

    /// <summary>Server reachability and version; the view shows its banner above the page.</summary>
    public ConnectionMonitor Connection { get; }

    /// <summary>Resource key of the portal title, e.g. "Portal.Admin".</summary>
    public string PortalTitleKey { get; }

    public IReadOnlyList<PortalPage> MenuItems { get; }

    public string UserName => _session.CurrentUser?.Username ?? string.Empty;

    /// <summary>The user's full name, or the username for accounts without one.</summary>
    public string DisplayName => _session.CurrentUser is { FullName: { Length: > 0 } fullName } ? fullName : UserName;

    /// <summary>Resource key of the user's role name, e.g. "Role.Super".</summary>
    public string RoleKey => $"Role.{_session.CurrentUser?.Role}";

    [ObservableProperty]
    public partial int SelectedMenuIndex { get; set; } = -1;

    /// <summary>The menu page, or the sub-page opened on top of it.</summary>
    [ObservableProperty]
    public partial ViewModelBase? CurrentPage { get; private set; }

    /// <summary>A page opened from the menu page (NavigationService.OpenAsync); the menu keeps its entry selected.</summary>
    public ViewModelBase? SubPage { get; private set; }

    partial void OnSelectedMenuIndexChanged(int oldValue, int newValue)
    {
        if (_restoringMenu || newValue < 0 || newValue >= MenuItems.Count)
        {
            return;
        }

        if (SubPage is FormViewModel { IsDirty: true } form)
        {
            _ = LeaveDirtySubPageAsync(form, oldValue, newValue);
            return;
        }

        ShowMenuPage(newValue);
    }

    /// <summary>Shows <paramref name="page"/> in place of the current page; false if the open sub-page kept unsaved changes.</summary>
    public async Task<bool> ShowSubPageAsync(ViewModelBase page)
    {
        if (SubPage is FormViewModel form && !await form.ConfirmDiscardAsync())
        {
            return false;
        }

        _menuPage ??= CurrentPage;
        Show(page);
        SubPage = page;
        return true;
    }

    /// <summary>Back to the menu page; false if the sub-page kept unsaved changes.</summary>
    public async Task<bool> CloseSubPageAsync()
    {
        if (SubPage is null)
        {
            return true;
        }

        if (SubPage is FormViewModel form && !await form.ConfirmDiscardAsync())
        {
            return false;
        }

        RestoreMenuPage();
        return true;
    }

    private async Task LeaveDirtySubPageAsync(FormViewModel form, int oldValue, int newValue)
    {
        if (await form.ConfirmDiscardAsync())
        {
            ShowMenuPage(newValue);
            return;
        }

        _restoringMenu = true;
        SelectedMenuIndex = oldValue;
        _restoringMenu = false;
    }

    private void ShowMenuPage(int index)
    {
        var pageType = MenuItems[index].PageType;
        if (!_navigation.CanAccess(pageType))
        {
            _session.SignOut(SignOutReason.Expired);
            return;
        }

        if (!_menuPages.TryGetValue(index, out var page))
        {
            page = _navigation.CreatePage(pageType);
            if (page is null)
            {
                _session.SignOut(SignOutReason.Expired);
                return;
            }

            _menuPages[index] = page;
        }

        _menuPage = page;
        SubPage = null;
        Show(page);
    }

    private void RestoreMenuPage()
    {
        var page = _menuPage;
        if (page is null)
        {
            ShowMenuPage(SelectedMenuIndex);
            return;
        }

        SubPage = null;
        Show(page);
    }

    private void Show(ViewModelBase page)
    {
        var name = page.GetType().Name;
        var nav = Stopwatch.StartNew();
        var outgoingName = CurrentPage?.GetType().Name ?? "none";

        (CurrentPage as ILoadablePage)?.Deactivate();
        _perf.Record("Nav", outgoingName, nav.Elapsed, $"deactivate {_perf.MachineSnapshot()}");

        // PageHost keeps views in the tree and toggles IsVisible (hide/show) — see View lines.
        var setPage = Stopwatch.StartNew();
        CurrentPage = page;
        _perf.Record("Nav", name, setPage.Elapsed, $"set-page {_perf.MachineSnapshot()}");
        _perf.Record("Nav", name, nav.Elapsed, $"total {_perf.MachineSnapshot()}");
        _perf.ProbeUiAfter(name, nav);

        if (page is ILoadablePage loadable)
        {
            _ = ActivateTimedAsync(page, loadable);
        }
        else
        {
            _perf.Record("Page", name, TimeSpan.Zero, "shown");
        }
    }

    private async Task ActivateTimedAsync(ViewModelBase page, ILoadablePage loadable)
    {
        using var scope = _perf.Measure("Page", page.GetType().Name);
        await loadable.ActivateAsync();
    }

    /// <summary>Called when the portal is replaced (e.g. sign-out): cancels the open page's API calls.</summary>
    public void Close() => (CurrentPage as ILoadablePage)?.Deactivate();

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        var form = new ChangePasswordFormViewModel(_authApi, _dialogs, _localization);
        if (await _dialogs.ShowFormAsync(form, _localization.Get("Auth.ChangePassword")))
        {
            _toasts.Success(_localization.Get("Auth.PasswordChanged"));
        }
    }

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
