using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private readonly ThemeService _theme;
    private string? _errorKey;

    public LoginViewModel(AuthApi authApi, SessionService session, NavigationService navigation, ThemeService theme)
    {
        _authApi = authApi;
        _session = session;
        _navigation = navigation;
        _theme = theme;

        if (session.LastSignOutReason == SignOutReason.Expired)
        {
            SetError("Login.SessionExpired");
        }

        LocalizationService.Instance.LanguageChanged += (_, _) => OnPropertyChanged(nameof(ErrorMessage));
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

    public string? ErrorMessage => _errorKey is null ? null : LocalizationService.Instance.Get(_errorKey);

    public bool HasError => _errorKey is not null;

#if DEBUG
    public bool IsDevBuild => true;
#else
    public bool IsDevBuild => false;
#endif

    private bool CanLogin() => !IsBusy && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrEmpty(Password);

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        SetError(null);
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
            SetError(result.Error switch
            {
                ApiError.Unauthorized => "Login.Invalid",
                ApiError.TooManyRequests => "Login.TooMany",
                ApiError.Unreachable => "Login.ServerUnreachable",
                _ => "Login.Failed",
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleTheme() => _theme.Toggle();

    [RelayCommand]
    private void ToggleLanguage() => LocalizationService.Instance.Toggle();

    [RelayCommand]
    private void OpenGallery() => _navigation.NavigateTo<GalleryViewModel>();

    private void SetError(string? key)
    {
        _errorKey = key;
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasError));
    }
}
