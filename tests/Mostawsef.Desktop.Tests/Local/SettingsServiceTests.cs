using Avalonia.Headless.XUnit;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Tests.Infrastructure;

namespace Mostawsef.Desktop.Tests.Local;

public class SettingsServiceTests
{
    [AvaloniaFact]
    public void Theme_and_language_changes_reach_the_settings_file()
    {
        using var app = new TestServices();

        app.Get<ThemeService>().SetTheme(AppTheme.Dark);
        app.Localization.SetLanguage(AppLanguage.Arabic);
        app.Get<SettingsService>().Flush();

        var saved = new SettingsService(app.SettingsFile).Current;
        Assert.Equal(AppTheme.Dark, saved.Theme);
        Assert.Equal(AppLanguage.Arabic, saved.Language);
    }

    [AvaloniaFact]
    public void Rapid_switches_leave_the_latest_values_on_disk()
    {
        using var app = new TestServices();
        var theme = app.Get<ThemeService>();

        for (var i = 0; i < 5; i++)
        {
            theme.Toggle();
            app.Localization.Toggle();
        }

        app.Get<SettingsService>().Flush();

        var saved = new SettingsService(app.SettingsFile).Current;
        Assert.Equal(AppTheme.Dark, saved.Theme);
        Assert.Equal(AppLanguage.Arabic, saved.Language);
    }

    [AvaloniaFact]
    public void A_locked_settings_file_does_not_block_or_break_switching()
    {
        using var app = new TestServices();

        using (File.Open(app.SettingsFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            app.Localization.SetLanguage(AppLanguage.Arabic);
            app.Get<ThemeService>().SetTheme(AppTheme.Dark);
            app.Get<SettingsService>().Flush();
        }

        Assert.Equal(AppLanguage.Arabic, app.Localization.Language);
        Assert.Equal(AppTheme.Dark, app.Get<ThemeService>().Current);
    }
}
