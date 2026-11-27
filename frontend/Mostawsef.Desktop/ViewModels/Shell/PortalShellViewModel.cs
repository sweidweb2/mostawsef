using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Printing;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Services.Updates;
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
    private readonly ErrorReporter _errors;
    private readonly PerformanceMonitor _perf;
    private readonly ClinicContext _clinic;
    private CancellationTokenSource? _updateCheck;

    /// <summary>Set while the menu goes back to its entry because the user kept a sub-page's changes.</summary>
    private bool _restoringMenu;

    /// <summary>Bumped when another menu change supersedes an in-flight dirty-leave confirm.</summary>
    private int _leaveGeneration;

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
        UpdateChecker updates,
        ClinicContext clinic,
        ErrorReporter errors,
        PerformanceMonitor perf)
    {
        _navigation = navigation;
        _session = session;
        _dialogs = dialogs;
        _localization = localization;
        _authApi = authApi;
        _toasts = toasts;
        _errors = errors;
        _perf = perf;
        _clinic = clinic;
        Connection = connection;
        Updates = updates;

        PortalTitleKey = portal.TitleKey;
        MenuItems = portal.Pages;
        Updates.ConfirmLeaveAsync = ConfirmLeaveForUpdateAsync;

        SelectedMenuIndex = 0;
    }

    private async Task<bool> ConfirmLeaveForUpdateAsync()
    {
        if (SubPage is FormViewModel { IsDirty: true } form)
        {
            return await form.ConfirmDiscardAsync();
        }

        return true;
    }

    /// <summary>
    /// Starts the portal's background work: server monitoring and, for Admin, the clinic details and print warm-up.
    /// Kept out of the constructor so building the view model (tests, tooling) has no I/O side effects;
    /// <see cref="NavigationService"/> calls it once after creating the portal.
    /// </summary>
    public void Start()
    {
        Connection.Start();
        _updateCheck?.Cancel();
        _updateCheck?.Dispose();
        _updateCheck = new CancellationTokenSource();
        var updateToken = _updateCheck.Token;
        _ = CheckForUpdatesAsync(updateToken);
        // Money/print need clinic details; Super never shows them. Single-flight in ClinicContext.
        if (_session.CurrentUser?.Role == Roles.Admin)
        {
            _ = _clinic.LoadAsync();
            PrintService.WarmUpInBackground();
        }
    }

    private async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Updates.CheckNowAsync(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Portal closed / signed out.
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: "Checking for app updates");
        }
    }

    /// <summary>Server reachability and version; the view shows its banner above the page.</summary>
    public ConnectionMonitor Connection { get; }

    /// <summary>GitHub release feed; banner + in-app Update (download → silent Setup → relaunch).</summary>
    public UpdateChecker Updates { get; }

    /// <summary>Resource key of the portal title, e.g. "Portal.Admin".</summary>
    public string PortalTitleKey { get; }

    public IReadOnlyList<PortalPage> MenuItems { get; }

    public string UserName => _session.CurrentUser?.Username ?? string.Empty;

    /// <summary>The user's full name, or the username for accounts without one.</summary>
    public string DisplayName => _session.CurrentUser is { FullName: { Length: > 0 } fullName } ? fullName : UserName;

    /// <summary>Resource key of the user's role name, e.g. "Role.Super".</summary>
    public string RoleKey => $"Role.{_session.CurrentUser?.Role}";

    /// <summary>Super can change password and uses a confirmed sign-out; the clinic Admin session only leaves back to the login screen.</summary>
    public bool CanManageAccount => _session.IsInRole(Roles.Super);

    /// <summary>Sidebar / menu label: Sign out for Super, Back to sign-in for the clinic session.</summary>
    public string LeaveKey => CanManageAccount ? "Auth.Logout" : "Auth.BackToSignIn";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MenuTitleKey))]
    public partial int SelectedMenuIndex { get; set; } = -1;

    /// <summary>Resource key of the selected menu page's title: the first breadcrumb segment.</summary>
    public string? MenuTitleKey =>
        SelectedMenuIndex >= 0 && SelectedMenuIndex < MenuItems.Count ? MenuItems[SelectedMenuIndex].TitleKey : null;

    /// <summary>The menu page, or the sub-page opened on top of it.</summary>
    [ObservableProperty]
    public partial ViewModelBase? CurrentPage { get; private set; }

    /// <summary>A page opened from the menu page (NavigationService.OpenAsync); the menu keeps its entry selected.</summary>
    public ViewModelBase? SubPage
    {
        get;
        private set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            if (field is IBreadcrumbPage old)
            {
                old.PropertyChanged -= OnSubPagePropertyChanged;
            }

            field = value;
            if (value is IBreadcrumbPage next)
            {
                next.PropertyChanged += OnSubPagePropertyChanged;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSubPage));
            OnPropertyChanged(nameof(SubPageTitleKey));
            OnPropertyChanged(nameof(SubPageTitleArg));
        }
    }

    /// <summary>True while a sub-page is open: the breadcrumb then shows "menu page &gt; sub-page".</summary>
    public bool HasSubPage => SubPage is not null;

    public string? SubPageTitleKey => (SubPage as IBreadcrumbPage)?.BreadcrumbKey;

    public string? SubPageTitleArg => (SubPage as IBreadcrumbPage)?.BreadcrumbArg;

    private void OnSubPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IBreadcrumbPage.BreadcrumbKey) or nameof(IBreadcrumbPage.BreadcrumbArg))
        {
            OnPropertyChanged(nameof(SubPageTitleKey));
            OnPropertyChanged(nameof(SubPageTitleArg));
        }
    }

    /// <summary>The breadcrumb's menu segment: closes the sub-page (asks first if it has unsaved changes).</summary>
    [RelayCommand]
    private Task<bool> BackAsync() => CloseSubPageAsync();

    /// <summary>Raised when a transient sub-page will not be shown again (PageHost should drop its view).</summary>
    public event Action<ViewModelBase>? PageDiscarded;

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

    /// <summary>
    /// Shows <paramref name="page"/> in place of the current page; false if the open sub-page kept unsaved changes.
    /// <paramref name="isNewInstance"/>: nobody keeps this instance for reuse, so its view is released when it closes.
    /// </summary>
    public async Task<bool> ShowSubPageAsync(ViewModelBase page, bool isNewInstance = false)
    {
        if (SubPage is FormViewModel form && !await form.ConfirmDiscardAsync())
        {
            return false;
        }

        DiscardSubPage();
        _menuPage ??= CurrentPage;
        _subPageIsNewInstance = isNewInstance;
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

    private void DiscardSubPage()
    {
        if (SubPage is null)
        {
            return;
        }

        var gone = SubPage;
        SubPage = null;
        if (gone is not IReusablePage || _subPageIsNewInstance)
        {
            PageDiscarded?.Invoke(gone);
        }
    }

    private bool _subPageIsNewInstance;

    private async Task LeaveDirtySubPageAsync(FormViewModel form, int oldValue, int newValue)
    {
        var leaveId = ++_leaveGeneration;
        var userId = _session.CurrentUser?.Id;

        try
        {
            if (await form.ConfirmDiscardAsync())
            {
                if (leaveId != _leaveGeneration || !_session.IsAuthenticated || _session.CurrentUser?.Id != userId)
                {
                    return;
                }

                ShowMenuPage(newValue);
                return;
            }
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: "Leaving dirty sub-page");
        }

        if (leaveId != _leaveGeneration || !_session.IsAuthenticated || _session.CurrentUser?.Id != userId)
        {
            return;
        }

        _restoringMenu = true;
        SelectedMenuIndex = oldValue;
        _restoringMenu = false;
    }

    private void ShowMenuPage(int index)
    {
        if (GetOrCreateMenuPage(index) is not { } page)
        {
            _session.SignOut(SignOutReason.Expired);
            return;
        }

        _menuPage = page;
        DiscardSubPage();
        Show(page);
    }

    /// <summary>
    /// The view model of a menu entry, created on first use and kept for the portal visit. It is not shown or
    /// activated here (no data loads), so the shell view can build its view ahead of time. Null when the user may not
    /// open that page.
    /// </summary>
    public ViewModelBase? GetOrCreateMenuPage(int index)
    {
        if (index < 0 || index >= MenuItems.Count)
        {
            return null;
        }

        if (_menuPages.TryGetValue(index, out var page))
        {
            return page;
        }

        var pageType = MenuItems[index].PageType;
        if (!_navigation.CanAccess(pageType) || _navigation.CreatePage(pageType) is not { } created)
        {
            return null;
        }

        _menuPages[index] = created;
        return created;
    }

    private void RestoreMenuPage()
    {
        var page = _menuPage;
        if (page is null)
        {
            ShowMenuPage(SelectedMenuIndex);
            return;
        }

        DiscardSubPage();
        Show(page);
    }

    private void Show(ViewModelBase page)
    {
        var name = page.GetType().Name;
        var track = _perf.IsEnabled;
        var nav = track ? Stopwatch.StartNew() : null;
        var outgoingName = CurrentPage?.GetType().Name ?? "none";

        (CurrentPage as ILoadablePage)?.Deactivate();
        if (track)
        {
            _perf.Record("Nav", outgoingName, nav!.Elapsed, $"deactivate {_perf.MachineSnapshot()}");
        }

        // PageHost keeps views in the tree and toggles IsVisible (hide/show) — see View lines.
        var setPage = track ? Stopwatch.StartNew() : null;
        CurrentPage = page;
        if (track)
        {
            _perf.Record("Nav", name, setPage!.Elapsed, $"set-page {_perf.MachineSnapshot()}");
            _perf.Record("Nav", name, nav!.Elapsed, $"total {_perf.MachineSnapshot()}");
            _perf.ProbeUiAfter(name, nav!);
        }

        if (page is ILoadablePage loadable)
        {
            _ = ActivateTimedAsync(page, loadable);
        }
        else if (track)
        {
            _perf.Record("Page", name, TimeSpan.Zero, "shown");
        }
    }

    private async Task ActivateTimedAsync(ViewModelBase page, ILoadablePage loadable)
    {
        try
        {
            using var scope = _perf.Measure("Page", page.GetType().Name);
            await loadable.ActivateAsync();
        }
        catch (Exception ex)
        {
            _errors.Report(ex, context: $"Activating {page.GetType().Name}");
        }
    }

    /// <summary>Called when the portal is replaced (e.g. sign-out): cancels open work and drops cached menu pages.</summary>
    public void Close()
    {
        _updateCheck?.Cancel();
        _updateCheck?.Dispose();
        _updateCheck = null;
        // UpdateChecker is a singleton; drop the method-group capture so this shell can be collected.
        if (ReferenceEquals(Updates.ConfirmLeaveAsync, (Func<Task<bool>>)ConfirmLeaveForUpdateAsync))
        {
            Updates.ConfirmLeaveAsync = null;
        }

        DiscardSubPage();
        (CurrentPage as ILoadablePage)?.Deactivate();
        foreach (var page in _menuPages.Values)
        {
            if (!ReferenceEquals(page, CurrentPage))
            {
                (page as ILoadablePage)?.Deactivate();
            }
        }

        _menuPages.Clear();
        _menuPage = null;
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        if (!CanManageAccount)
        {
            return;
        }

        var form = new ChangePasswordFormViewModel(_authApi, _session, _dialogs, _localization);
        if (await _dialogs.ShowFormAsync(form, _localization.Get("Auth.ChangePassword")))
        {
            _toasts.Success(_localization.Get("Auth.PasswordChanged"));
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        if (!CanManageAccount)
        {
            // Best-effort activity log; the token is discarded either way.
            await _authApi.LogoutAsync();
            _session.SignOut();
            return;
        }

        if (await _dialogs.ConfirmAsync(_localization.Get("Auth.LogoutConfirm"), confirmText: _localization.Get("Auth.Logout")))
        {
            await _authApi.LogoutAsync();
            _session.SignOut();
        }
    }
}
