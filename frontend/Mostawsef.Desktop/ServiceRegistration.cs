using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Printing;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels;

namespace Mostawsef.Desktop;

/// <summary>Overrides used by tests: a fake HTTP handler and an isolated settings file.</summary>
public class DesktopServiceOptions
{
    public HttpMessageHandler? HttpHandler { get; init; }

    public string? SettingsFilePath { get; init; }

    /// <summary>Opens a printed PDF; the default opens it in the system viewer.</summary>
    public Action<string>? OpenFile { get; init; }
}

public static class ServiceRegistration
{
    /// <summary>Longest wait for a whole API call (connect, server work, response).</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Longest wait to open a connection, so a switched-off API PC fails fast instead of after RequestTimeout.</summary>
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);

    /// <summary>The real network handler under the API HttpClient.</summary>
    public static SocketsHttpHandler CreateServerHandler() => new() { ConnectTimeout = ConnectTimeout };

    public static IServiceCollection AddMostawsefDesktop(this IServiceCollection services, DesktopServiceOptions? options = null)
    {
        options ??= new DesktopServiceOptions();

        // Local
        services.AddSingleton(_ => options.SettingsFilePath is null ? new SettingsService() : new SettingsService(options.SettingsFilePath));
        services.AddSingleton<ThemeService>();
        services.AddSingleton<LocalizationService>();

        // API
        services.AddSingleton(sp =>
        {
            var auth = new AuthHeaderHandler(sp.GetRequiredService<SessionService>())
            {
                InnerHandler = options.HttpHandler ?? CreateServerHandler(),
            };
            var http = new HttpClient(auth);
            http.BaseAddress = new Uri(sp.GetRequiredService<SettingsService>().Current.ApiBaseUrl.TrimEnd('/') + "/");
            http.Timeout = RequestTimeout;
            return http;
        });
        // HttpClient stays uncreated until the first API call (login, health, or error report).
        services.AddSingleton(sp => new ApiClient(
            new Lazy<HttpClient>(sp.GetRequiredService<HttpClient>),
            sp.GetRequiredService<PerformanceMonitor>()));
        services.AddSingleton<AuthApi>();
        services.AddSingleton<LogsApi>();
        services.AddSingleton<UsersApi>();
        services.AddSingleton<ClinicSettingsApi>();
        services.AddSingleton<SystemSettingsApi>();
        services.AddSingleton<LabTestsApi>();
        services.AddSingleton<PatientsApi>();
        services.AddSingleton<OrdersApi>();
        services.AddSingleton<ConnectionMonitor>();

        // Diagnostics: system errors + optional performance overlay/log
        // Lazy deps so Install() does not build the HTTP stack or toast service.
        services.AddSingleton(sp => new ErrorReporter(
            new Lazy<LogsApi>(sp.GetRequiredService<LogsApi>),
            new Lazy<NavigationService>(sp.GetRequiredService<NavigationService>),
            new Lazy<IToastService>(sp.GetRequiredService<IToastService>),
            new Lazy<LocalizationService>(sp.GetRequiredService<LocalizationService>)));
        services.AddSingleton<PerformanceMonitor>();

        // Clinic details + printing
        services.AddSingleton<ClinicContext>();
        services.AddSingleton(sp => new PrintService(
            sp.GetRequiredService<LocalizationService>(),
            sp.GetRequiredService<ClinicContext>(),
            options.OpenFile ?? PrintService.OpenWithDefaultApp,
            sp.GetRequiredService<PerformanceMonitor>()));

        // Auth + navigation
        services.AddSingleton<SessionService>();
        services.AddSingleton(AppRoutes.Table);
        services.AddSingleton<NavigationService>();

        // UI
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());
        services.AddSingleton<ToastService>();
        services.AddSingleton<IToastService>(sp => sp.GetRequiredService<ToastService>());

        // View models: pages come from AppRoutes; portal shells are created by NavigationService.
        services.AddSingleton<MainWindowViewModel>();
        foreach (var pageType in AppRoutes.Table.PageTypes)
        {
            services.AddTransient(pageType);
        }

        return services;
    }
}
