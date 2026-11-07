using System;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.ViewModels;
using Mostawsef.Desktop.ViewModels.Admin;
using Mostawsef.Desktop.ViewModels.Auth;
using Mostawsef.Desktop.ViewModels.Super;

namespace Mostawsef.Desktop.Services.Navigation;

/// <summary>
/// Top-level navigation (login screen and portal shells) with route protection:
/// pages marked [RequiresRole] / [RequiresAuthentication] are never shown to the wrong user.
/// </summary>
public partial class NavigationService : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly SessionService _session;

    public NavigationService(IServiceProvider services, SessionService session)
    {
        _services = services;
        _session = session;
        _session.Changed += (_, _) =>
        {
            if (!_session.IsAuthenticated)
            {
                NavigateTo<LoginViewModel>();
            }
        };
    }

    [ObservableProperty]
    public partial ViewModelBase? CurrentPage { get; private set; }

    /// <summary>Opens the page if the current user may see it; otherwise falls back to the login screen.</summary>
    public bool NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        if (!CanAccess(typeof(TViewModel)))
        {
            CurrentPage = _services.GetRequiredService<LoginViewModel>();
            return false;
        }

        CurrentPage = _services.GetRequiredService<TViewModel>();
        return true;
    }

    /// <summary>Opens the portal that matches the signed-in user's role.</summary>
    public void NavigateToHome()
    {
        if (_session.IsInRole(Roles.Super))
        {
            NavigateTo<SuperShellViewModel>();
        }
        else if (_session.IsInRole(Roles.Admin))
        {
            NavigateTo<AdminShellViewModel>();
        }
        else
        {
            NavigateTo<LoginViewModel>();
        }
    }

    public bool CanAccess(Type viewModelType)
    {
        var roles = viewModelType.GetCustomAttribute<RequiresRoleAttribute>()?.Roles;
        if (roles is { Length: > 0 })
        {
            return roles.Any(_session.IsInRole);
        }

        return viewModelType.GetCustomAttribute<RequiresAuthenticationAttribute>() is null || _session.IsAuthenticated;
    }
}
