using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Auth;
using Mostawsef.Desktop.Views.Auth;
using Mostawsef.Desktop.Views.Shell;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;

namespace Mostawsef.Desktop.Tests.Views;

/// <summary>Renders real views headlessly (no window on screen) and checks what the user would see.</summary>
[Collection(UiTestCollection.Name)]
public class ViewRenderingTests : UiTestBase
{
    [AvaloniaFact]
    public void Login_screen_shows_the_sign_in_form()
    {
        using var app = new TestServices();
        var window = app.Show(app.Get<LoginViewModel>());

        Assert.NotNull(window.GetVisualDescendants().OfType<LoginView>().SingleOrDefault());
        Assert.NotNull(window.FindDescendantOfType<PasswordField>());
        Assert.NotNull(window.FindDescendantOfType<PreferencesBar>());
        Assert.Contains(Texts(window), t => t == "Sign in");
        Assert.Contains(Texts(window), t => t == "Enter clinic");
        Assert.Contains(Texts(window), t => t == $"Version {app.Get<LoginViewModel>().Version}");
    }

    [AvaloniaFact]
    public async Task Long_login_error_wraps_inside_the_form()
    {
        using var app = new TestServices();
        app.Api.FailWithNetworkError();
        var vm = app.Get<LoginViewModel>();
        vm.Username = "user@test.local";
        vm.Password = "secret";
        var window = app.Show(vm);

        await vm.LoginCommand.ExecuteAsync(null);
        window.UpdateLayout();

        var form = window.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "FormPanel");
        var message = app.Localization.Get(vm.ErrorKey!);
        var error = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == message);
        var errorRight = error.TranslatePoint(new Point(error.Bounds.Width, 0), form)!.Value.X;

        Assert.True(errorRight <= form.Bounds.Width, $"Error text ends at {errorRight}, form is {form.Bounds.Width} wide");
    }

    [AvaloniaFact]
    public void Wide_login_shows_the_brand_panel_beside_the_form()
    {
        using var app = new TestServices();
        var window = app.Show(app.Get<LoginViewModel>());
        window.UpdateLayout();

        var panel = Named<Border>(window, "BrandPanel");
        var form = Named<Grid>(window, "FormSide");

        Assert.True(panel.IsVisible);
        Assert.Contains("Your clinic, organized in one place", Texts(window));
        Assert.True(form.Bounds.Width < window.Bounds.Width, "The form should share the width with the brand panel");
    }

    [AvaloniaFact]
    public void Narrow_login_hides_the_brand_panel_and_gives_the_form_the_full_width_in_both_languages()
    {
        using var app = new TestServices();
        var window = app.Show(app.Get<LoginViewModel>());
        window.Width = 720;
        window.Height = 520;
        Relayout(window);

        var panel = Named<Border>(window, "BrandPanel");
        var form = Named<Grid>(window, "FormSide");
        Assert.False(panel.IsVisible);
        Assert.Equal(window.Bounds.Width, form.Bounds.Width);

        app.Localization.SetLanguage(AppLanguage.Arabic);
        Relayout(window);
        Assert.False(panel.IsVisible);
        Assert.Contains("مرحبًا بعودتك", Texts(window));

        window.Width = 1280;
        Relayout(window);
        Assert.True(panel.IsVisible);
    }

    private static T Named<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    // SizeChanged is raised after the layout pass; the view's breakpoint switch then needs one more pass.
    private static void Relayout(Window window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    [AvaloniaFact]
    public void Login_inputs_show_translated_placeholders()
    {
        using var app = new TestServices();
        var window = app.Show(app.Get<LoginViewModel>());
        var username = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "UsernameBox");
        var password = window.FindDescendantOfType<PasswordField>()!;

        Assert.Equal("Enter your username", username.PlaceholderText);
        Assert.Equal("Enter your password", password.PlaceholderText);

        app.Localization.SetLanguage(AppLanguage.Arabic);

        Assert.Equal("أدخل اسم المستخدم", username.PlaceholderText);
        Assert.Equal("أدخل كلمة المرور", password.PlaceholderText);
    }

    [AvaloniaFact]
    public void Admin_portal_renders_the_shared_shell_with_the_admin_dashboard()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin, "admin@test.local");
        app.Navigation.NavigateToHome();

        var window = app.Show(app.Navigation.CurrentPage!);

        Assert.NotNull(window.FindDescendantOfType<PortalShellView>());
        Assert.NotNull(window.FindDescendantOfType<DashboardView>());
        Assert.DoesNotContain(Texts(window), t => t == "Admin portal");
        Assert.Equal("Dashboard", window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "BreadcrumbCurrent").Text);
        Assert.Contains(Texts(window), t => t == "Welcome, admin@test.local");
    }

    [AvaloniaFact]
    public void Super_portal_renders_the_shared_shell_with_the_super_dashboard()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Navigation.NavigateToHome();

        var window = app.Show(app.Navigation.CurrentPage!);

        Assert.NotNull(window.FindDescendantOfType<Mostawsef.Desktop.Views.Super.SuperDashboardView>());
        Assert.DoesNotContain(Texts(window), t => t == "Super portal");
        Assert.Equal("Dashboard", window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "BreadcrumbCurrent").Text);
        Assert.Contains(Texts(window), t => t == "Welcome, user@test.local");
    }

    [AvaloniaFact]
    public void Portal_title_menu_and_welcome_follow_the_selected_language()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin, "admin@test.local");
        app.Navigation.NavigateToHome();
        var window = app.Show(app.Navigation.CurrentPage!);

        app.Localization.SetLanguage(AppLanguage.Arabic);

        var texts = Texts(window);
        Assert.Contains(app.Localization.Get("Nav.Dashboard"), texts);
        Assert.Contains(app.Localization.Format("Dashboard.Welcome", "admin@test.local"), texts);
        Assert.Contains(app.Localization.Get("Nav.Dashboard"), window.GetVisualDescendants().OfType<NavMenuItem>().Select(i => i.Text));
        Assert.DoesNotContain("Admin portal", texts);
    }

    [AvaloniaFact]
    public void Arabic_switches_the_window_to_right_to_left_and_translates_text()
    {
        using var app = new TestServices(AppLanguage.Arabic);
        var window = app.Show(app.Get<LoginViewModel>());

        Assert.Equal(FlowDirection.RightToLeft, window.FlowDirection);
        Assert.Contains(Texts(window), t => t == "تسجيل الدخول");

        app.Localization.SetLanguage(AppLanguage.English);

        Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);
        Assert.Contains(Texts(window), t => t == "Sign in");
    }

    [AvaloniaFact]
    public void Login_shows_the_large_brand_mark_and_the_portal_sidebar_the_small_one()
    {
        using var app = new TestServices();
        var login = app.Show(app.Get<LoginViewModel>());
        app.SignInAs(Roles.Admin);
        app.Navigation.NavigateToHome();
        var portal = app.Show(app.Navigation.CurrentPage!);

        var large = Tile(login);
        var small = Tile(portal);

        Assert.Equal(56, large.Bounds.Width);
        Assert.Equal(32, small.Bounds.Width);
        Assert.Contains("Mostawsef", Texts(login));
        Assert.Contains("Mostawsef", Texts(portal));

        static Border Tile(Window window) =>
            window.FindDescendantOfType<BrandMark>()!.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_Tile");
    }

    [AvaloniaFact]
    public void Closed_portals_are_not_kept_alive_by_language_changes()
    {
        using var app = new TestServices();
        var (shell, dashboard) = OpenPortalThenSignOut(app);

        app.Localization.SetLanguage(AppLanguage.Arabic);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(shell.TryGetTarget(out _), "Portal shell is still alive");
        Assert.False(dashboard.TryGetTarget(out _), "Dashboard is still alive");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<object> Shell, WeakReference<object> Dashboard) OpenPortalThenSignOut(TestServices app)
    {
        app.SignInAs(Roles.Admin);
        app.Navigation.NavigateToHome();
        var shell = (ViewModels.Shell.PortalShellViewModel)app.Navigation.CurrentPage!;
        var result = (new WeakReference<object>(shell), new WeakReference<object>(shell.CurrentPage!));
        app.Session.SignOut();
        return result;
    }
}
