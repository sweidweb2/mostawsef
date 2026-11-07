using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Globalization;
using System.Resources;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;

namespace Mostawsef.Desktop.Services.Local;

/// <summary>
/// Runtime language switching. A DI singleton; code not created by DI (markup, controls) uses App.Localization.
/// </summary>
public class LocalizationService : INotifyPropertyChanged
{
    private static readonly ResourceManager Resources =
        new("Mostawsef.Desktop.Resources.Strings", typeof(LocalizationService).Assembly);

    private readonly List<WeakReference<Window>> _windows = [];
    private readonly SettingsService _settings;

    public LocalizationService(SettingsService settings)
    {
        _settings = settings;
        ApplyCulture(settings.Current.Language);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public AppLanguage Language { get; private set; } = AppLanguage.English;

    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-US");

    public bool IsRightToLeft => Language == AppLanguage.Arabic;

    public FlowDirection FlowDirection => IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// <summary>
    /// Incremented on every language change; {l:Tr}/{l:TrKey} bindings observe it to refresh translated text.
    /// Only controls attached to the visual tree should subscribe to LanguageChanged (and unsubscribe on detach).
    /// </summary>
    public int Version { get; private set; }

    public string this[string key] => Get(key);

    public string Get(string key) => Resources.GetString(key, Culture) ?? key;

    public string Format(string key, params object?[] args) => string.Format(Culture, Get(key), args);

    public void SetLanguage(AppLanguage language)
    {
        if (language == Language)
        {
            return;
        }

        ApplyCulture(language);
        _settings.Current.Language = language;
        _settings.Save();

        Version++;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRightToLeft)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlowDirection)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Version)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle() => SetLanguage(Language == AppLanguage.Arabic ? AppLanguage.English : AppLanguage.Arabic);

    /// <summary>Applies the current flow direction to a window and keeps it updated on language changes. Call for every window you create.</summary>
    public void Apply(Window window)
    {
        window.FlowDirection = FlowDirection;

        if (!_windows.Any(w => w.TryGetTarget(out var existing) && ReferenceEquals(existing, window)))
        {
            _windows.Add(new WeakReference<Window>(window));
        }
    }

    private void ApplyCulture(AppLanguage language)
    {
        Language = language;

        // ar-EG keeps the Gregorian calendar and Latin digits while using Arabic month/day names.
        Culture = CultureInfo.GetCultureInfo(language == AppLanguage.Arabic ? "ar-EG" : "en-US");
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;

        _windows.RemoveAll(w => !w.TryGetTarget(out _));
        foreach (var reference in _windows)
        {
            if (reference.TryGetTarget(out var window))
            {
                window.FlowDirection = FlowDirection;
            }
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var window in desktop.Windows)
            {
                Apply(window);
            }
        }
    }
}
