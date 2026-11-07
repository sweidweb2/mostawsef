using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;

namespace Mostawsef.Desktop.Services.Local;

/// <summary>
/// Runtime language switching. Exposed as a singleton so XAML markup ({l:Tr Key}) can reach it.
/// </summary>
public class LocalizationService : INotifyPropertyChanged
{
    private static readonly ResourceManager Resources =
        new("Mostawsef.Desktop.Resources.Strings", typeof(LocalizationService).Assembly);

    private SettingsService? _settings;

    public static LocalizationService Instance { get; } = new();

    private LocalizationService()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public AppLanguage Language { get; private set; } = AppLanguage.English;

    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-US");

    public bool IsRightToLeft => Language == AppLanguage.Arabic;

    public FlowDirection FlowDirection => IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// <summary>Incremented on every language change; bindings observe it to refresh translated text.</summary>
    public int Version { get; private set; }

    public string this[string key] => Get(key);

    public void Initialize(SettingsService settings)
    {
        _settings = settings;
        ApplyCulture(settings.Current.Language);
    }

    public string Get(string key) => Resources.GetString(key, Culture) ?? key;

    public string Format(string key, params object?[] args) => string.Format(Culture, Get(key), args);

    public void SetLanguage(AppLanguage language)
    {
        if (language == Language)
        {
            return;
        }

        ApplyCulture(language);

        if (_settings is not null)
        {
            _settings.Current.Language = language;
            _settings.Save();
        }

        Version++;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRightToLeft)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlowDirection)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Version)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle() => SetLanguage(Language == AppLanguage.Arabic ? AppLanguage.English : AppLanguage.Arabic);

    /// <summary>Applies the current flow direction to a window. Call for every window you create.</summary>
    public void Apply(Window window) => window.FlowDirection = FlowDirection;

    private void ApplyCulture(AppLanguage language)
    {
        Language = language;

        // ar-EG keeps the Gregorian calendar and Latin digits while using Arabic month/day names.
        Culture = CultureInfo.GetCultureInfo(language == AppLanguage.Arabic ? "ar-EG" : "en-US");
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var window in desktop.Windows)
            {
                Apply(window);
            }
        }
    }
}
