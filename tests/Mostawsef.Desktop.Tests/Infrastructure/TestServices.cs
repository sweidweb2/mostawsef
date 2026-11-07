using Avalonia.Controls;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;

namespace Mostawsef.Desktop.Tests.Infrastructure;

/// <summary>
/// The app's real service graph with a fake API and a throwaway settings file (English, Light).
/// Becomes App.Services for the test, so {l:Tr} and controls use this test's own LocalizationService.
/// Must be created on the Avalonia UI thread, i.e. inside an [AvaloniaFact] test.
/// </summary>
public sealed class TestServices : IDisposable
{
    private readonly string _settingsDirectory = Path.Combine(Path.GetTempPath(), "mostawsef-tests", Guid.NewGuid().ToString("N"));

    public TestServices(AppLanguage language = AppLanguage.English)
    {
        var settingsFile = Path.Combine(_settingsDirectory, "settings.json");
        Directory.CreateDirectory(_settingsDirectory);
        File.WriteAllText(settingsFile, $$"""{ "Theme": "Light", "Language": "{{language}}" }""");

        Provider = new ServiceCollection()
            .AddMostawsefDesktop(new DesktopServiceOptions { HttpHandler = Api, SettingsFilePath = settingsFile })
            .BuildServiceProvider();
        App.Services = Provider;
        Get<ThemeService>().Initialize();
    }

    public FakeApiHandler Api { get; } = new();

    public ServiceProvider Provider { get; }

    public SessionService Session => Provider.GetRequiredService<SessionService>();

    public NavigationService Navigation => Provider.GetRequiredService<NavigationService>();

    public LocalizationService Localization => Provider.GetRequiredService<LocalizationService>();

    public T Get<T>() where T : notnull => Provider.GetRequiredService<T>();

    public void SignInAs(string role, string username = "user@test.local") =>
        Session.SignIn(FakeApiHandler.LoginResponseFor(role, username));

    /// <summary>Shows content (usually a view model, resolved to its view) in a headless window.</summary>
    public Window Show(object content)
    {
        var window = new Window { Width = 1280, Height = 800, Content = content };
        Localization.Apply(window);
        window.Show();
        return window;
    }

    /// <summary>Texts of all visible TextBlocks in the window.</summary>
    public static List<string> Texts(Window window) =>
        window.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible)
            .Select(t => t.Text ?? string.Empty)
            .ToList();

    public void Dispose()
    {
        Provider.Dispose();
        Directory.Delete(_settingsDirectory, recursive: true);
    }
}
