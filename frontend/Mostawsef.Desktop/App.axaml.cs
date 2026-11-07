using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.ViewModels;
using Mostawsef.Desktop.ViewModels.Auth;
using Mostawsef.Desktop.Views;

namespace Mostawsef.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; internal set; } = null!;

    /// <summary>The DI LocalizationService, for code that DI does not create ({l:Tr}, controls).</summary>
    public static LocalizationService Localization => Services.GetRequiredService<LocalizationService>();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Only the real desktop app builds services and windows; headless tests use the styles alone.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Services = new ServiceCollection().AddMostawsefDesktop().BuildServiceProvider();
            Services.GetRequiredService<ErrorReporter>().Install();
            Services.GetRequiredService<ThemeService>().Initialize();
            desktop.Exit += (_, _) => Services.GetRequiredService<SettingsService>().Flush();

            var window = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
            Localization.Apply(window);
            desktop.MainWindow = window;

            Services.GetRequiredService<NavigationService>().NavigateTo<LoginViewModel>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
