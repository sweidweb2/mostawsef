using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Auth;

namespace Mostawsef.Desktop.Tests.Views;

public class PortalShellViewTests
{
    [AvaloniaFact]
    public void Clicking_the_avatar_opens_a_menu_with_change_password_and_sign_out()
    {
        using var app = new TestServices();
        var window = ShowPortal(app, Roles.Admin);

        var flyout = OpenUserMenu(window);

        Assert.True(flyout.IsOpen);
        Assert.Equal(["Change password", "Sign out"], flyout.Items.OfType<MenuItem>().Select(i => i.Header));
    }

    [AvaloniaFact]
    public void Sign_out_from_the_avatar_menu_asks_to_confirm_then_returns_to_login()
    {
        using var app = new TestServices();
        var window = ShowPortal(app, Roles.Super);
        var item = OpenUserMenu(window).Items.OfType<MenuItem>().Single(i => i.Name == "LogoutMenuItem");

        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var confirm = window.GetVisualDescendants().OfType<ConfirmDialog>().Single();
        Assert.True(app.Session.IsAuthenticated);

        confirm.Close(true);
        Dispatcher.UIThread.RunJobs();

        Assert.False(app.Session.IsAuthenticated);
        Assert.Equal(SignOutReason.UserRequested, app.Session.LastSignOutReason);
        Assert.IsType<LoginViewModel>(app.Navigation.CurrentPage);
    }

    [AvaloniaFact]
    public void Sidebar_sign_out_asks_to_confirm_and_cancel_keeps_the_user_signed_in()
    {
        using var app = new TestServices();
        var window = ShowPortal(app, Roles.Admin);
        var signOut = window.GetVisualDescendants().OfType<NavMenuItem>().Single(i => i.Text == "Sign out");
        var center = signOut.TranslatePoint(new Point(signOut.Bounds.Width / 2, signOut.Bounds.Height / 2), window)!.Value;

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        var confirm = window.GetVisualDescendants().OfType<ConfirmDialog>().Single();
        confirm.Close(false);
        Dispatcher.UIThread.RunJobs();

        Assert.True(app.Session.IsAuthenticated);
        Assert.Empty(window.GetVisualDescendants().OfType<ConfirmDialog>());
    }

    [AvaloniaTheory]
    [InlineData(AppLanguage.English)]
    [InlineData(AppLanguage.Arabic)]
    public void Collapsed_sidebar_toggle_lines_up_with_the_menu_icons(AppLanguage language)
    {
        using var app = new TestServices(language);
        var window = ShowPortal(app, Roles.Admin);
        var shell = window.FindDescendantOfType<AppShell>()!;

        shell.IsSidebarCompact = true;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var toggle = shell.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "PART_Toggle");
        var menuItem = shell.GetVisualDescendants().OfType<NavMenuItem>().First();
        Assert.Equal(CenterX(menuItem, window), CenterX(toggle, window), precision: 0);
    }

    private static Window ShowPortal(TestServices app, string role)
    {
        app.SignInAs(role);
        app.Navigation.NavigateToHome();

        var host = new DialogHost { Content = new ContentControl { Content = app.Navigation.CurrentPage } };
        app.Get<DialogService>().Attach(host);
        return app.Show(host);
    }

    private static MenuFlyout OpenUserMenu(Window window)
    {
        var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "UserMenuButton");
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        return Assert.IsType<MenuFlyout>(button.Flyout);
    }

    private static double CenterX(Visual visual, Visual relativeTo) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, 0), relativeTo)!.Value.X;
}
