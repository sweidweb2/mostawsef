using System;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Printing;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Services.Updates;
using Mostawsef.Desktop.ViewModels;

namespace Mostawsef.Desktop;

/// <summary>Overrides used by tests: a fake HTTP handler and an isolated settings file.</summary>
public class DesktopServiceOptions
{
    public HttpMessageHandler? HttpHandler { get; init; }

    /// <summary>HTTP for the public update feed (GitHub). Tests supply a fake; production uses the network.</summary>
    public HttpMessageHandler? UpdateHttpHandler { get; init; }

    /// <summary>Override the update feed URL (tests).</summary>
    public Uri? UpdateFeedUrl { get; init; }

    /// <summary>Process/path hooks for in-app Setup install (tests supply a fake).</summary>
    public IUpdateHost? UpdateHost { get; init; }

    public string? SettingsFilePath { get; init; }

    /// <summary>Opens a printed PDF; the default opens it in the system viewer.</summary>
    public Action<string>? OpenFile { get; init; }

    /// <summary>Asks where to save an exported file; the default shows the system "Save as" dialog.</summary>
    public SavePathPicker? PickSavePath { get; init; }

    /// <summary>Asks the user to pick an existing file; the default shows the system open dialog.</summary>
    public OpenPathPicker? PickOpenPath { get; init; }

    /// <summary>Asks for a folder (backup path); the default shows the system folder dialog.</summary>
    public FolderPathPicker? PickFolderPath { get; init; }
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

        // Local (Lazy ErrorReporter: SettingsService is built before the reporter, and Load() may run in the ctor)
        services.AddSingleton(sp => new SettingsService(
            options.SettingsFilePath
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mostawsef", "settings.json"),
            new Lazy<ErrorReporter>(sp.GetRequiredService<ErrorReporter>)));
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
            // ApiClient applies RequestTimeout per call (longer for whole-table work), so the client itself has no limit.
            http.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
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
        services.AddSingleton<SystemApi>();
        services.AddSingleton<BackupsApi>();
        services.AddSingleton<ScheduledDumpsApi>();
        services.AddSingleton<LabTestsApi>();
        services.AddSingleton<PatientsApi>();
        services.AddSingleton<OrdersApi>();
        services.AddSingleton<ConnectionMonitor>();
        services.AddSingleton<IUpdateHost>(_ => options.UpdateHost ?? new UpdateHost());
        services.AddSingleton(sp => new UpdateChecker(
            options.UpdateHttpHandler ?? CreateServerHandler(),
            sp.GetRequiredService<LocalizationService>(),
            sp.GetRequiredService<IUpdateHost>(),
            options.UpdateFeedUrl));

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
        services.AddSingleton<LabTestCatalogVersion>();
        services.AddSingleton<LabTestCatalog>();
        services.AddSingleton(sp => new PrintService(
            sp.GetRequiredService<LocalizationService>(),
            sp.GetRequiredService<ClinicContext>(),
            options.OpenFile ?? PrintService.OpenWithDefaultApp,
            sp.GetRequiredService<PerformanceMonitor>()));

        services.AddSingleton<SavePathPicker>(options.PickSavePath ?? SaveFilePicker.PickAsync);
        services.AddSingleton<OpenPathPicker>(options.PickOpenPath ?? OpenFilePicker.PickAsync);
        services.AddSingleton<FolderPathPicker>(options.PickFolderPath ?? FolderPicker.PickAsync);
        services.AddSingleton(sp => new InvoiceService(
            sp.GetRequiredService<OrdersApi>(),
            sp.GetRequiredService<PrintService>(),
            sp.GetRequiredService<IToastService>(),
            sp.GetRequiredService<LocalizationService>(),
            sp.GetRequiredService<ErrorReporter>(),
            sp.GetRequiredService<SavePathPicker>()));

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
