using System.Net;
using Avalonia.Headless.XUnit;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Admin;
using Mostawsef.Desktop.ViewModels.Auth;
using Mostawsef.Desktop.ViewModels.Super;

namespace Mostawsef.Desktop.Tests.Auth;

public class LoginViewModelTests
{
    [AvaloniaFact]
    public void Sign_in_is_disabled_until_username_and_password_are_entered()
    {
        using var app = new TestServices();
        var vm = app.Get<LoginViewModel>();

        Assert.False(vm.LoginCommand.CanExecute(null));

        vm.Username = "admin@test.local";
        Assert.False(vm.LoginCommand.CanExecute(null));

        vm.Password = "secret";
        Assert.True(vm.LoginCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Admin_sign_in_opens_the_admin_portal()
    {
        using var app = new TestServices();
        app.Api.RespondToLoginWith(Roles.Admin);
        var vm = Filled(app.Get<LoginViewModel>());

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.IsType<AdminShellViewModel>(app.Navigation.CurrentPage);
        Assert.True(app.Session.IsInRole(Roles.Admin));
        Assert.Empty(vm.Password);
    }

    [AvaloniaFact]
    public async Task Super_sign_in_opens_the_super_portal()
    {
        using var app = new TestServices();
        app.Api.RespondToLoginWith(Roles.Super);
        var vm = Filled(app.Get<LoginViewModel>());

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.IsType<SuperShellViewModel>(app.Navigation.CurrentPage);
    }

    [AvaloniaFact]
    public async Task Wrong_credentials_show_an_error_clear_the_password_and_stay_signed_out()
    {
        using var app = new TestServices();
        app.Api.RespondWithStatus(HttpStatusCode.Unauthorized);
        var vm = Filled(app.Get<LoginViewModel>());

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Equal("Invalid username or password.", vm.ErrorMessage);
        Assert.Empty(vm.Password);
        Assert.False(app.Session.IsAuthenticated);
    }

    [AvaloniaFact]
    public async Task Unreachable_server_shows_a_connection_error()
    {
        using var app = new TestServices();
        app.Api.FailWithNetworkError();
        var vm = Filled(app.Get<LoginViewModel>());

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.StartsWith("Cannot reach the server", vm.ErrorMessage);
    }

    [AvaloniaFact]
    public async Task Too_many_attempts_show_a_wait_message()
    {
        using var app = new TestServices();
        app.Api.RespondWithStatus(HttpStatusCode.TooManyRequests);
        var vm = Filled(app.Get<LoginViewModel>());

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.StartsWith("Too many attempts", vm.ErrorMessage);
    }

    [AvaloniaFact]
    public async Task Error_message_follows_the_selected_language()
    {
        using var app = new TestServices();
        app.Api.RespondWithStatus(HttpStatusCode.Unauthorized);
        var vm = Filled(app.Get<LoginViewModel>());
        await vm.LoginCommand.ExecuteAsync(null);

        Services.Local.LocalizationService.Instance.SetLanguage(Services.Local.AppLanguage.Arabic);

        Assert.Equal("اسم المستخدم أو كلمة المرور غير صحيحة.", vm.ErrorMessage);
    }

    private static LoginViewModel Filled(LoginViewModel vm)
    {
        vm.Username = "user@test.local";
        vm.Password = "secret";
        return vm;
    }
}
