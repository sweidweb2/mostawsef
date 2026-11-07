using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Auth;
using Mostawsef.Desktop.Views.Admin;
using Mostawsef.Desktop.Views.Auth;
using Mostawsef.Desktop.Views.Shell;
using Mostawsef.Desktop.Views.Super;

namespace Mostawsef.Desktop.Tests.Views;

/// <summary>Renders real views headlessly (no window on screen) and checks what the user would see.</summary>
public class ViewRenderingTests
{
    [AvaloniaFact]
    public void Login_screen_shows_the_sign_in_form()
    {
        using var app = new TestServices();
        var window = Show(app.Get<LoginViewModel>());

        Assert.NotNull(window.GetVisualDescendants().OfType<LoginView>().SingleOrDefault());
        Assert.NotNull(window.FindDescendantOfType<PasswordField>());
        Assert.Contains(Texts(window), t => t == "Sign in");
    }

    [AvaloniaFact]
    public async Task Long_login_error_wraps_inside_the_card()
    {
        using var app = new TestServices();
        app.Api.FailWithNetworkError();
        var vm = app.Get<LoginViewModel>();
        vm.Username = "user@test.local";
        vm.Password = "secret";
        var window = Show(vm);

        await vm.LoginCommand.ExecuteAsync(null);
        window.UpdateLayout();

        var card = window.FindDescendantOfType<Card>()!;
        var error = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == vm.ErrorMessage);
        var errorRight = error.TranslatePoint(new Point(error.Bounds.Width, 0), card)!.Value.X;

        Assert.True(errorRight <= card.Bounds.Width, $"Error text ends at {errorRight}, card is {card.Bounds.Width} wide");
    }

    [AvaloniaFact]
    public void Login_inputs_show_translated_placeholders()
    {
        using var app = new TestServices();
        var window = Show(app.Get<LoginViewModel>());
        var username = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "UsernameBox");
        var password = window.FindDescendantOfType<PasswordField>()!;

        Assert.Equal("Enter your username", username.PlaceholderText);
        Assert.Equal("Enter your password", password.PlaceholderText);

        LocalizationService.Instance.SetLanguage(AppLanguage.Arabic);

        Assert.Equal("أدخل اسم المستخدم", username.PlaceholderText);
        Assert.Equal("أدخل كلمة المرور", password.PlaceholderText);
    }

    [AvaloniaFact]
    public void Admin_portal_renders_the_shared_shell_with_the_admin_dashboard()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin, "admin@test.local");
        app.Navigation.NavigateToHome();

        var window = Show(app.Navigation.CurrentPage!);

        Assert.NotNull(window.FindDescendantOfType<PortalShellView>());
        Assert.NotNull(window.FindDescendantOfType<AdminDashboardView>());
        Assert.Null(window.FindDescendantOfType<SuperDashboardView>());
        Assert.Contains(Texts(window), t => t == "Admin portal");
        Assert.Contains(Texts(window), t => t == "Welcome, admin@test.local");
    }

    [AvaloniaFact]
    public void Super_portal_renders_the_shared_shell_with_the_super_dashboard()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Navigation.NavigateToHome();

        var window = Show(app.Navigation.CurrentPage!);

        Assert.NotNull(window.FindDescendantOfType<SuperDashboardView>());
        Assert.Contains(Texts(window), t => t == "Super portal");
    }

    [AvaloniaFact]
    public void Arabic_switches_the_window_to_right_to_left_and_translates_text()
    {
        using var app = new TestServices(AppLanguage.Arabic);
        var window = Show(app.Get<LoginViewModel>());

        Assert.Equal(FlowDirection.RightToLeft, window.FlowDirection);
        Assert.Contains(Texts(window), t => t == "تسجيل الدخول");

        LocalizationService.Instance.SetLanguage(AppLanguage.English);

        Assert.Equal(FlowDirection.LeftToRight, window.FlowDirection);
        Assert.Contains(Texts(window), t => t == "Sign in");
    }

    private static Window Show(object viewModel)
    {
        var window = new Window
        {
            Width = 1280,
            Height = 800,
            Content = new ContentControl { Content = viewModel },
        };
        LocalizationService.Instance.Apply(window);
        window.Show();
        return window;
    }

    private static List<string> Texts(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible)
            .Select(t => t.Text ?? string.Empty)
            .ToList();
}
