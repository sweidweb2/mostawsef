using System;
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

        CurrentPage = ActivatorUtilities.CreateInstance<PortalShellViewModel>(_services, portal);
        return true;
    }

    public bool CanAccess(Type viewModelType) => _routes.IsAllowed(viewModelType, _session.IsInRole);

    /// <summary>Creates the page if the current user may open it, otherwise returns null.</summary>
    public ViewModelBase? CreatePage(Type viewModelType) =>
        CanAccess(viewModelType) ? (ViewModelBase)_services.GetRequiredService(viewModelType) : null;

    private void ShowLogin() => CurrentPage = _services.GetRequiredService<LoginViewModel>();
}
