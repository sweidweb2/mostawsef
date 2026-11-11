using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Auth;
using static Mostawsef.Desktop.Tests.Infrastructure.TestServices;

namespace Mostawsef.Desktop.Tests.Views;

[Collection(UiTestCollection.Name)]
public class PreferencesBarTests : UiTestBase
{
    [AvaloniaFact]
    public void Theme_button_switches_between_light_and_dark_and_saves_it()
    {
        using var app = new TestServices();
        var window = app.Show(new PreferencesBar());

        Click(window, "PART_Theme");

        Assert.Equal(AppTheme.Dark, app.Get<ThemeService>().Current);
        Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current!.RequestedThemeVariant);
        Assert.Equal(AppTheme.Dark, app.Get<SettingsService>().Current.Theme);

        Click(window, "PART_Theme");

        Assert.Equal(AppTheme.Light, app.Get<ThemeService>().Current);
    }

    [AvaloniaFact]
    public void Language_button_switches_to_arabic_and_mirrors_the_window()
    {
        using var app = new TestServices();
        var window = app.Show(new PreferencesBar());
        Assert.Contains("العربية", Texts(window));

        Click(window, "PART_Language");

        Assert.Equal(AppLanguage.Arabic, app.Localization.Language);
        Assert.Equal(AppLanguage.Arabic, app.Get<SettingsService>().Current.Language);
        Assert.Equal(Avalonia.Media.FlowDirection.RightToLeft, window.FlowDirection);
        Assert.Contains("English", Texts(window));
    }

    [AvaloniaFact]
    public void Login_screen_and_both_portals_use_the_shared_bar()
    {
        using var app = new TestServices();
        Assert.Single(app.Show(app.Get<LoginViewModel>()).GetVisualDescendants().OfType<PreferencesBar>());

        foreach (var role in new[] { Roles.Admin, Roles.Super })
        {
            app.SignInAs(role);
            app.Navigation.NavigateToHome();
            Assert.Single(app.Show(app.Navigation.CurrentPage!).GetVisualDescendants().OfType<PreferencesBar>());
        }
    }

    private static void Click(Window window, string partName)
    {
        var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == partName);
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }
}
