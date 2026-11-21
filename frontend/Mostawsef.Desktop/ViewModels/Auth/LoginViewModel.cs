using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;

namespace Mostawsef.Desktop.ViewModels.Auth;

public partial class LoginViewModel : ViewModelBase
{
    private readonly AuthApi _authApi;
    private readonly SessionService _session;
    private readonly NavigationService _navigation;

    public LoginViewModel(AuthApi authApi, SessionService session, NavigationService navigation, SettingsService settings)
    {
        ShowInsecureConnectionWarning = ApiAddress.IsInsecureRemote(settings.Current.ApiBaseUrl);
        _authApi = authApi;
        _session = session;
        _navigation = navigation;

        if (session.LastSignOutReason == SignOutReason.Expired)
        {
            ErrorKey = "Login.SessionExpired";
        }

        _ = RefreshClinicAccessAsync();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    [NotifyCanExecuteChangedFor(nameof(EnterClinicCommand))]
    public partial string Username { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    [NotifyCanExecuteChangedFor(nameof(EnterClinicCommand))]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    [NotifyCanExecuteChangedFor(nameof(EnterClinicCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>False while maintenance mode is on (Enter clinic stays disabled). True until the first access check fails closed only after a known maintenance response.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnterClinicCommand))]
    [NotifyPropertyChangedFor(nameof(ShowClinicUnavailable))]
    public partial bool IsClinicEntryAvailable { get; private set; } = true;

    public bool ShowClinicUnavailable => !IsClinicEntryAvailable;

    /// <summary>Resource key of the error to show (translated by the view), or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorKey { get; private set; }

    public bool HasError => ErrorKey is not null;

    /// <summary>The API address is plain http to another machine, so the password would cross the network unencrypted.</summary>
    public bool ShowInsecureConnectionWarning { get; }

    private bool CanLogin() => !IsBusy && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrEmpty(Password);

    private bool CanEnterClinic() => !IsBusy && IsClinicEntryAvailable;

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        ErrorKey = null;
        IsBusy = true;

        try
        {
            var result = await _authApi.LoginAsync(Username.Trim(), Password);
            if (result.IsSuccess)
            {
                _session.SignIn(result.Value!);
                Password = string.Empty;
                _navigation.NavigateToHome();
                return;
            }

            Password = string.Empty;
            ErrorKey = result.Error switch
            {
                ApiError.Unauthorized => "Login.Invalid",
                ApiError.TooManyRequests => "Login.TooMany",
                ApiError.Unreachable => "Login.ServerUnreachable",
                _ => "Login.Failed",
            };
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEnterClinic))]
    private async Task EnterClinicAsync()
    {
        ErrorKey = null;
        IsBusy = true;

        try
        {
            var result = await _authApi.OpenClinicSessionAsync();
            if (result.IsSuccess)
            {
                _session.SignIn(result.Value!);
                Password = string.Empty;
                _navigation.NavigateToHome();
                return;
            }

            if (result.Error == ApiError.Conflict && result.Problem?.Code == ErrorCodes.MaintenanceMode)
            {
                IsClinicEntryAvailable = false;
                ErrorKey = "Login.Maintenance";
                return;
            }

            ErrorKey = result.Error switch
            {
                ApiError.Forbidden => "Login.ClinicForbidden",
                ApiError.TooManyRequests => "Login.TooMany",
                ApiError.Unreachable => "Login.ServerUnreachable",
                _ => "Login.Failed",
            };
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshClinicAccessAsync()
    {
        var result = await _authApi.GetClinicAccessAsync();
        if (result.IsSuccess)
        {
            IsClinicEntryAvailable = result.Value!.Available;
        }
        // Unreachable / other errors: leave the button enabled so a click surfaces Login.ServerUnreachable.
    }
}
