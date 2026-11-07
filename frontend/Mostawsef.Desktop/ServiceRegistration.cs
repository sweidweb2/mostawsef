using System;
using System.Net.Http;
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

namespace Mostawsef.Desktop;

/// <summary>Overrides used by tests: a fake HTTP handler and an isolated settings file.</summary>
public class DesktopServiceOptions
{
    public HttpMessageHandler? HttpHandler { get; init; }

    public string? SettingsFilePath { get; init; }
}

public static class ServiceRegistration
{
    public static IServiceCollection AddMostawsefDesktop(this IServiceCollection services, DesktopServiceOptions? options = null)
    {
        options ??= new DesktopServiceOptions();

        // Local
        services.AddSingleton(_ => options.SettingsFilePath is null ? new SettingsService() : new SettingsService(options.SettingsFilePath));
        services.AddSingleton<ThemeService>();
        services.AddSingleton(LocalizationService.Instance);

        // API
        services.AddSingleton(sp =>
        {
            var http = options.HttpHandler is null ? new HttpClient() : new HttpClient(options.HttpHandler);
            http.BaseAddress = new Uri(sp.GetRequiredService<SettingsService>().Current.ApiBaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(15);
            return http;
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

        return services;
    }
}
