using System.Net;
using Avalonia.Headless.XUnit;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Auth;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.Tests.Auth;

[Collection(UiTestCollection.Name)]
public class LoginViewModelTests : UiTestBase
{
    [AvaloniaFact]
    public void Sign_in_is_disabled_until_username_and_password_are_entered()
    {
        using var app = new TestServices();
        var vm = app.Get<LoginViewModel>();
        vm.Username = string.Empty;
        vm.Password = string.Empty;

        Assert.False(vm.LoginCommand.CanExecute(null));

        vm.Username = "admin@test.local";
        Assert.False(vm.LoginCommand.CanExecute(null));

        vm.Password = "secret";
        Assert.True(vm.LoginCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void Only_debug_builds_prefill_the_development_admin_login()
    {
        using var app = new TestServices();
        var vm = app.Get<LoginViewModel>();

        Assert.Equal(vm.IsDevBuild ? "admin@example.com" : string.Empty, vm.Username);
        Assert.Equal(vm.IsDevBuild ? "password" : string.Empty, vm.Password);
    }

    [AvaloniaFact]
    public async Task Admin_sign_in_opens_the_admin_portal()
    {
        using var app = new TestServices();
        app.Api.RespondToLoginWith(Roles.Admin);
        var vm = Filled(app.Get<LoginViewModel>());

        await vm.LoginCommand.ExecuteAsync(null);

        var shell = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
        Assert.Equal("Portal.Admin", shell.PortalTitleKey);
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

        var shell = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
        Assert.Equal("Portal.Super", shell.PortalTitleKey);
    }

    [AvaloniaFact]
    public async Task Wrong_credentials_show_an_error_clear_the_password_and_stay_signed_out()
    {
        using var app = new TestServices();
        app.Api.RespondWithStatus(HttpStatusCode.Unauthorized);
        var vm = Filled(app.Get<LoginViewModel>());
        var window = app.Show(vm);

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Equal("Login.Invalid", vm.ErrorKey);
        Assert.Contains("Invalid username or password.", TestServices.Texts(window));
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

        Assert.Equal("Login.ServerUnreachable", vm.ErrorKey);
        Assert.StartsWith("Cannot reach the server", app.Localization.Get(vm.ErrorKey!));
    }

    [AvaloniaFact]
    public async Task Too_many_attempts_show_a_wait_message()
    {
        using var app = new TestServices();
        app.Api.RespondWithStatus(HttpStatusCode.TooManyRequests);
        var vm = Filled(app.Get<LoginViewModel>());

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Equal("Login.TooMany", vm.ErrorKey);
        Assert.StartsWith("Too many attempts", app.Localization.Get(vm.ErrorKey!));
    }

    [AvaloniaFact]
    public async Task Maintenance_mode_shows_a_maintenance_message_and_stays_signed_out()
    {
        using var app = new TestServices();
        app.Api.Respond = _ => FakeApiHandler.Problem(HttpStatusCode.Conflict, ErrorCodes.MaintenanceMode);
        var vm = Filled(app.Get<LoginViewModel>());

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Equal("Login.Maintenance", vm.ErrorKey);
        Assert.Equal("The system is under maintenance. Please try again later.", app.Localization.Get(vm.ErrorKey!));
        Assert.False(app.Session.IsAuthenticated);
    }

    [AvaloniaFact]
    public async Task Error_message_follows_the_selected_language()
    {
        using var app = new TestServices();
        app.Api.RespondWithStatus(HttpStatusCode.Unauthorized);
        var vm = Filled(app.Get<LoginViewModel>());
        var window = app.Show(vm);
        await vm.LoginCommand.ExecuteAsync(null);

        app.Localization.SetLanguage(AppLanguage.Arabic);

        Assert.Contains("اسم المستخدم أو كلمة المرور غير صحيحة.", TestServices.Texts(window));
    }

    private static LoginViewModel Filled(LoginViewModel vm)
    {
        vm.Username = "user@test.local";
        vm.Password = "secret";
        return vm;
    }
}
