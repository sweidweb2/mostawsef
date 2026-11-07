using System;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels;
using Mostawsef.Desktop.ViewModels.Admin;
using Mostawsef.Desktop.ViewModels.Auth;
using Mostawsef.Desktop.ViewModels.Dev;
using Mostawsef.Desktop.ViewModels.Super;
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
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
            LocalizationService.Instance.Apply(window);
            desktop.MainWindow = window;

            Services.GetRequiredService<NavigationService>().NavigateTo<LoginViewModel>();
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

        // API
        services.AddSingleton(sp => new HttpClient
        {
            BaseAddress = new Uri(sp.GetRequiredService<SettingsService>().Current.ApiBaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(15),
        });
        services.AddSingleton<ApiClient>();
        services.AddSingleton<AuthApi>();

        // Auth + navigation
        services.AddSingleton<SessionService>();
        services.AddSingleton<NavigationService>();

        // UI
        services.AddSingleton<DialogService>();
        services.AddSingleton<ToastService>();

        // View models
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<AdminShellViewModel>();
        services.AddTransient<AdminDashboardViewModel>();
        services.AddTransient<SuperShellViewModel>();
        services.AddTransient<SuperDashboardViewModel>();
        services.AddTransient<GalleryViewModel>();

        return services.BuildServiceProvider();
    }
}
