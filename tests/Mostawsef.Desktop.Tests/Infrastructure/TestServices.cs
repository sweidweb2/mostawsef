using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;

namespace Mostawsef.Desktop.Tests.Infrastructure;

/// <summary>
/// The app's real service graph with a fake API and a throwaway settings file (English, Light).
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

        LocalizationService.Instance.Initialize(Provider.GetRequiredService<SettingsService>());
        LocalizationService.Instance.SetLanguage(language);
    }

    public FakeApiHandler Api { get; } = new();

    public ServiceProvider Provider { get; }

    public SessionService Session => Provider.GetRequiredService<SessionService>();

    public NavigationService Navigation => Provider.GetRequiredService<NavigationService>();

    public T Get<T>() where T : notnull => Provider.GetRequiredService<T>();

    public void SignInAs(string role, string username = "user@test.local") =>
        Session.SignIn(FakeApiHandler.LoginResponseFor(role, username));

    public void Dispose()
    {
        Provider.Dispose();
        Directory.Delete(_settingsDirectory, recursive: true);
    }
}
