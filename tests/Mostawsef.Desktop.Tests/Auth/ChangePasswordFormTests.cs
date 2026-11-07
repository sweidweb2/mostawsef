using System.Net;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Auth;

namespace Mostawsef.Desktop.Tests.Auth;

public class ChangePasswordFormTests
{
    private const string ChangePasswordPath = "/api/auth/change-password";

    [AvaloniaTheory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Super)]
    public void Every_portal_opens_change_password_from_the_user_menu(string role)
    {
        using var app = new TestServices();
        var window = ShowPortal(app, role);

        ClickChangePassword(window);

        var modal = window.GetVisualDescendants().OfType<Modal>().Single();
        Assert.Equal("Change password", modal.Title);
        Assert.IsType<ChangePasswordFormViewModel>(modal.Content);
    }

    [AvaloniaFact]
    public async Task A_wrong_current_password_from_the_server_shows_on_that_field()
    {
        using var app = new TestServices();
        app.Api.On(HttpMethod.Post, ChangePasswordPath, _ =>
            FakeApiHandler.Problem(HttpStatusCode.BadRequest, ErrorCodes.Validation, ("CurrentPassword", ErrorCodes.WrongPassword)));
        var window = ShowPortal(app, Roles.Admin);
        var form = OpenForm(window);

        form.CurrentPassword = "not-it";
        form.NewPassword = "Changed#Pass7";
        form.ConfirmPassword = "Changed#Pass7";
        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        var field = window.GetVisualDescendants().OfType<FormField>().Single(f => f.Label == "Current password");
        Assert.Equal("The current password is wrong.", field.Error);
        Assert.Single(window.GetVisualDescendants().OfType<Modal>());
    }

    [AvaloniaFact]
    public async Task A_successful_change_closes_the_form_and_confirms()
    {
        using var app = new TestServices();
        app.Api.On(HttpMethod.Post, ChangePasswordPath, HttpStatusCode.NoContent);
        var window = ShowPortal(app, Roles.Super);
        var form = OpenForm(window);

        form.CurrentPassword = "Old#Pass1";
        form.NewPassword = "Changed#Pass7";
        form.ConfirmPassword = "Changed#Pass7";
        await form.SaveCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(app.Api.RequestsTo(HttpMethod.Post, ChangePasswordPath));
        Assert.Empty(window.GetVisualDescendants().OfType<Modal>());
        Assert.Contains("Password changed.", window.GetVisualDescendants().OfType<Toast>().Select(t => t.Message));
    }

    private static Window ShowPortal(TestServices app, string role)
    {
        app.SignInAs(role);
        app.Navigation.NavigateToHome();
        var host = new DialogHost { Content = new ContentControl { Content = app.Navigation.CurrentPage } };
        app.Get<DialogService>().Attach(host);
        var window = app.Show(host);
        app.Get<ToastService>().Attach(window);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void ClickChangePassword(Window window)
    {
        var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "UserMenuButton");
        var menu = Assert.IsType<MenuFlyout>(button.Flyout);
        menu.ShowAt(button);
        Dispatcher.UIThread.RunJobs();
        menu.Items.OfType<MenuItem>().Single(i => i.Name == "ChangePasswordMenuItem").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static ChangePasswordFormViewModel OpenForm(Window window)
    {
        ClickChangePassword(window);
        return (ChangePasswordFormViewModel)window.GetVisualDescendants().OfType<Modal>().Single().Content!;
    }
}
