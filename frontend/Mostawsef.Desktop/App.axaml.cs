using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Dev;
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
        Services = ConfigureServices();

        var settings = Services.GetRequiredService<SettingsService>();
        LocalizationService.Instance.Initialize(settings);
        Services.GetRequiredService<ThemeService>().Initialize();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow
            {
                DataContext = Services.GetRequiredService<GalleryViewModel>(),
            };
            LocalizationService.Instance.Apply(window);
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Local
        services.AddSingleton<SettingsService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton(LocalizationService.Instance);

        // UI
        services.AddSingleton<DialogService>();
        services.AddSingleton<ToastService>();

        // View models
        services.AddTransient<GalleryViewModel>();

        return services.BuildServiceProvider();
    }
}
