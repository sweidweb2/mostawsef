using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.ViewModels.Dev;

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

#if DEBUG
        // Development seed account (appsettings.Development.json); never compiled into Release builds.
        Username = "admin@example.com";
        Password = "password";
#endif
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    public partial string Username { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>Resource key of the error to show (translated by the view), or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorKey { get; private set; }

    public bool HasError => ErrorKey is not null;

    /// <summary>The API address is plain http to another machine, so the password would cross the network unencrypted.</summary>
    public bool ShowInsecureConnectionWarning { get; }

#if DEBUG
    public bool IsDevBuild => true;
#else
    public bool IsDevBuild => false;
#endif

    private bool CanLogin() => !IsBusy && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrEmpty(Password);

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
                ApiError.Conflict when result.Problem?.Code == ErrorCodes.MaintenanceMode => "Login.Maintenance",
                ApiError.Unreachable => "Login.ServerUnreachable",
                _ => "Login.Failed",
            };
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenGallery() => _navigation.NavigateTo<GalleryViewModel>();
}
