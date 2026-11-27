using System.Net;
using System.Net.Http;
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
    public void Login_exposes_the_product_version_from_the_desktop_assembly()
    {
        using var app = new TestServices();
        var vm = app.Get<LoginViewModel>();

        Assert.False(string.IsNullOrWhiteSpace(vm.Version));
        Assert.Equal(AppVersion.Current, vm.Version);
        Assert.Matches(@"^\d+\.\d+\.\d+", vm.Version);
    }

    [AvaloniaFact]
    public void Sign_in_is_disabled_until_username_and_password_are_entered()
    {
        using var app = new TestServices();
        var vm = app.Get<LoginViewModel>();

        Assert.False(vm.LoginCommand.CanExecute(null));

        vm.Username = "super@test.local";
        Assert.False(vm.LoginCommand.CanExecute(null));

        vm.Password = "secret";
        Assert.True(vm.LoginCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Login_starts_with_empty_username_and_password_and_clinic_entry_enabled()
    {
        using var app = new TestServices();
        app.Api.RespondToClinicAccess(available: true);
        var vm = app.Get<LoginViewModel>();
        await UiWait.UntilAsync(() => vm.IsClinicEntryAvailable && vm.EnterClinicCommand.CanExecute(null));

        Assert.Empty(vm.Username);
        Assert.Empty(vm.Password);
        Assert.True(vm.IsClinicEntryAvailable);
    }

    [AvaloniaFact]
    public async Task Enter_clinic_opens_the_admin_portal()
    {
        using var app = new TestServices();
        app.Api.RespondToClinicAccess(available: true);
        app.Api.RespondToClinicSession();
        var vm = app.Get<LoginViewModel>();
        await UiWait.UntilAsync(() => vm.EnterClinicCommand.CanExecute(null));

        await vm.EnterClinicCommand.ExecuteAsync(null);

        var shell = Assert.IsType<PortalShellViewModel>(app.Navigation.CurrentPage);
        Assert.Equal("Portal.Admin", shell.PortalTitleKey);
        Assert.True(app.Session.IsInRole(Roles.Admin));
        Assert.Equal(ClinicIdentity.Username, app.Session.CurrentUser!.Username);
    }

    [AvaloniaFact]
    public async Task Super_sign_in_opens_the_super_portal()
    {
        using var app = new TestServices();
        app.Api.RespondToClinicAccess(available: true);
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
        app.Api.RespondToClinicAccess(available: true);
        app.Api.RespondWithStatus(HttpStatusCode.Unauthorized);
        var vm = Filled(app.Get<LoginViewModel>());
        var window = app.Show(vm);

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Equal("Login.Invalid", vm.ErrorKey);
        window.UpdateLayout();
        Assert.Contains("Invalid username or password.", TestServices.Texts(window));
        Assert.Empty(vm.Password);
        Assert.False(app.Session.IsAuthenticated);
    }

    [AvaloniaFact]
    public async Task Unreachable_server_shows_a_connection_error()
    {
        using var app = new TestServices();
        app.Api.RespondToClinicAccess(available: true);
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
        app.Api.RespondToClinicAccess(available: true);
        app.Api.RespondWithStatus(HttpStatusCode.TooManyRequests);
        var vm = Filled(app.Get<LoginViewModel>());

        await vm.LoginCommand.ExecuteAsync(null);

        Assert.Equal("Login.TooMany", vm.ErrorKey);
        Assert.StartsWith("Too many attempts", app.Localization.Get(vm.ErrorKey!));
    }

    [AvaloniaFact]
    public async Task Maintenance_mode_from_clinic_access_disables_enter_clinic()
    {
        using var app = new TestServices();
        app.Api.RespondToClinicAccess(available: false);
        var vm = app.Get<LoginViewModel>();
        await UiWait.UntilAsync(() => !vm.IsClinicEntryAvailable);

        Assert.False(vm.EnterClinicCommand.CanExecute(null));
        Assert.True(vm.ShowClinicUnavailable);
    }

    [AvaloniaFact]
    public async Task Enter_clinic_during_maintenance_shows_a_maintenance_message_and_stays_signed_out()
    {
        using var app = new TestServices();
        app.Api.RespondToClinicAccess(available: true);
        app.Api.On(HttpMethod.Post, "/api/auth/clinic-session",
            _ => FakeApiHandler.Problem(HttpStatusCode.Conflict, ErrorCodes.MaintenanceMode));
        var vm = app.Get<LoginViewModel>();
        await UiWait.UntilAsync(() => vm.EnterClinicCommand.CanExecute(null));

        await vm.EnterClinicCommand.ExecuteAsync(null);

        Assert.Equal("Login.Maintenance", vm.ErrorKey);
        Assert.Equal("The system is under maintenance. Please try again later.", app.Localization.Get(vm.ErrorKey!));
        Assert.False(vm.IsClinicEntryAvailable);
        Assert.False(app.Session.IsAuthenticated);
    }

    [AvaloniaFact]
    public async Task Enter_clinic_re_enables_after_maintenance_ends_while_login_stays_open()
    {
        var previous = LoginViewModel.ClinicAccessPollInterval;
        LoginViewModel.ClinicAccessPollInterval = TimeSpan.FromMilliseconds(50);
        try
        {
            using var app = new TestServices();
            var available = false;
            app.Api.On(HttpMethod.Get, "/api/auth/clinic-access",
                _ => FakeApiHandler.Json(new ClinicAccessResponse(available)));
            var vm = app.Get<LoginViewModel>();
            await UiWait.UntilAsync(() => !vm.IsClinicEntryAvailable);

            available = true;
            await UiWait.UntilAsync(() => vm.IsClinicEntryAvailable && vm.EnterClinicCommand.CanExecute(null));

            Assert.False(vm.ShowClinicUnavailable);
        }
        finally
        {
            LoginViewModel.ClinicAccessPollInterval = previous;
        }
    }

    [AvaloniaFact]
    public async Task Error_message_follows_the_selected_language()
    {
        using var app = new TestServices();
        app.Api.RespondToClinicAccess(available: true);
        app.Api.RespondWithStatus(HttpStatusCode.Unauthorized);
        var vm = Filled(app.Get<LoginViewModel>());
        var window = app.Show(vm);
        await vm.LoginCommand.ExecuteAsync(null);

        app.Localization.SetLanguage(AppLanguage.Arabic);
        window.UpdateLayout();

        Assert.Contains("اسم المستخدم أو كلمة المرور غير صحيحة.", TestServices.Texts(window));
    }

    private static LoginViewModel Filled(LoginViewModel vm)
    {
        vm.Username = "user@test.local";
        vm.Password = "secret";
        return vm;
    }
}
