using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Services.Local;

namespace Mostawsef.Desktop.Components;

/// <summary>Theme and language switches. Acts on the app's ThemeService and LocalizationService directly.</summary>
public class PreferencesBar : TemplatedControl
{
    public static readonly DirectProperty<PreferencesBar, ICommand> ToggleThemeCommandProperty =
        AvaloniaProperty.RegisterDirect<PreferencesBar, ICommand>(nameof(ToggleThemeCommand), o => o.ToggleThemeCommand);

    public static readonly DirectProperty<PreferencesBar, ICommand> ToggleLanguageCommandProperty =
        AvaloniaProperty.RegisterDirect<PreferencesBar, ICommand>(nameof(ToggleLanguageCommand), o => o.ToggleLanguageCommand);

    public ICommand ToggleThemeCommand { get; } =
        new RelayCommand(() => App.Services.GetRequiredService<ThemeService>().Toggle());

    public ICommand ToggleLanguageCommand { get; } = new RelayCommand(() => App.Localization.Toggle());
}
