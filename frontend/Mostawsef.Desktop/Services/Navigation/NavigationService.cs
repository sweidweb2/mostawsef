using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.ViewModels;
using Mostawsef.Desktop.ViewModels.Auth;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.Services.Navigation;

/// <summary>
/// Top-level navigation (login screen, role portals) and the single access check for every page,
/// driven by the <see cref="RouteTable"/>: a page is only created for a user its route allows.
/// </summary>
public partial class NavigationService : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly SessionService _session;
    private readonly RouteTable _routes;

    public NavigationService(IServiceProvider services, SessionService session, RouteTable routes)
    {
        _services = services;
        _session = session;
        _routes = routes;
        _session.Changed += (_, _) =>
        {
            if (!_session.IsAuthenticated)
            {
                ShowLogin();
            }
        };
    }

    [ObservableProperty]
    public partial ViewModelBase? CurrentPage { get; private set; }

    partial void OnCurrentPageChanged(ViewModelBase? oldValue, ViewModelBase? newValue) =>
        (oldValue as PortalShellViewModel)?.Close();

    /// <summary>Opens the page if the current user may see it; otherwise falls back to the login screen.</summary>
    public bool NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        var page = CreatePage(typeof(TViewModel));
        if (page is null)
        {
            ShowLogin();
            return false;
        }

        CurrentPage = page;
        return true;
    }

    /// <summary>Opens the portal that matches the signed-in user's role.</summary>
    public void NavigateToHome() => NavigateToPortal(_session.CurrentUser?.Role);

    /// <summary>Opens the role's portal if the current user has that role; otherwise falls back to the login screen.</summary>
    public bool NavigateToPortal(string? role)
    {
        var portal = _routes.FindPortal(role);
        if (portal is null || !_session.IsInRole(portal.Role))
        {
            ShowLogin();
            return false;
        }

        var shell = ActivatorUtilities.CreateInstance<PortalShellViewModel>(_services, portal);
        CurrentPage = shell;
        shell.Start();
        return true;
    }

    /// <summary>
    /// Opens a sub-page of the current portal (e.g. a details page) in place of the shown page; <paramref name="setup"/>
    /// passes its parameters before it loads. False if the user may not open it or kept unsaved changes of the open sub-page.
    /// </summary>
    public Task<bool> OpenAsync<TViewModel>(Action<TViewModel>? setup = null) where TViewModel : ViewModelBase
    {
        if (CurrentPage is not PortalShellViewModel shell || CreatePage(typeof(TViewModel)) is not TViewModel page)
        {
            return Task.FromResult(false);
        }

        // A new instance nobody holds on to: the shell must release its view when it closes, even for a reusable page type.
        setup?.Invoke(page);
        return shell.ShowSubPageAsync(page, isNewInstance: true);
    }

    /// <summary>
    /// Opens an existing sub-page instance (e.g. a reused New order page). False if the user may not open it or kept
    /// unsaved changes of the open sub-page.
    /// </summary>
    public async Task<bool> OpenAsync<TViewModel>(TViewModel page, Action<TViewModel>? setup = null)
        where TViewModel : ViewModelBase
    {
        if (CurrentPage is not PortalShellViewModel shell || !CanAccess(typeof(TViewModel)))
        {
            return false;
        }

        setup?.Invoke(page);
        return await shell.ShowSubPageAsync(page);
    }

    /// <summary>Closes the open sub-page and returns to the menu page (asks first if it has unsaved changes).</summary>
    public Task<bool> BackAsync() =>
        CurrentPage is PortalShellViewModel shell ? shell.CloseSubPageAsync() : Task.FromResult(false);

    public bool CanAccess(Type viewModelType) => _routes.IsAllowed(viewModelType, _session.IsInRole);

    /// <summary>
    /// The one reusable instance of a sub-page for its opener: created on first use into <paramref name="cache"/>
    /// (null if the current user may not open it), so PageHost can keep reusing its view.
    /// </summary>
    public TViewModel? Reuse<TViewModel>(ref TViewModel? cache) where TViewModel : ViewModelBase =>
        cache ??= CreatePage(typeof(TViewModel)) as TViewModel;

    /// <summary>Creates the page if the current user may open it, otherwise returns null.</summary>
    public ViewModelBase? CreatePage(Type viewModelType) =>
        CanAccess(viewModelType) ? (ViewModelBase)_services.GetRequiredService(viewModelType) : null;

    private void ShowLogin() => CurrentPage = _services.GetRequiredService<LoginViewModel>();
}
