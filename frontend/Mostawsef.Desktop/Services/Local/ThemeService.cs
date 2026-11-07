using System;
using Avalonia;
using Avalonia.Styling;

namespace Mostawsef.Desktop.Services.Local;

public class ThemeService(SettingsService settings)
{
    public AppTheme Current { get; private set; } = settings.Current.Theme;

    public event EventHandler<AppTheme>? ThemeChanged;

    public void Initialize() => Apply(Current);

    public void SetTheme(AppTheme theme)
    {
        if (theme == Current)
        {
            return;
        }

        Current = theme;
        settings.Current.Theme = theme;
        settings.Save();
        Apply(theme);
        ThemeChanged?.Invoke(this, theme);
    }

    public void Toggle() => SetTheme(Current == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);

    private static void Apply(AppTheme theme)
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = theme == AppTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}
