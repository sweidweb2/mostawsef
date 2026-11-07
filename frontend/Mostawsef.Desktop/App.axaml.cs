using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.ViewModels;
using Mostawsef.Desktop.ViewModels.Auth;
using Mostawsef.Desktop.Views;

namespace Mostawsef.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

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

            LocalizationService.Instance.Initialize(Services.GetRequiredService<SettingsService>());
            Services.GetRequiredService<ThemeService>().Initialize();

            var window = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
            LocalizationService.Instance.Apply(window);
            desktop.MainWindow = window;

            Services.GetRequiredService<NavigationService>().NavigateTo<LoginViewModel>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
