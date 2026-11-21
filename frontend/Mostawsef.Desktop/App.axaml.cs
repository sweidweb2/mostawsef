using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Diagnostics;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Components;
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

        // Developer Tools walk the visual tree on every attach/detach. That showed up as multi-second
        // Nav stalls + GC storms in Debug, so it is opt-in: set MOSTAWSEF_DEVTOOLS=1.
#if DEBUG
        if (string.Equals(Environment.GetEnvironmentVariable("MOSTAWSEF_DEVTOOLS"), "1", StringComparison.Ordinal))
        {
            this.AttachDeveloperTools();
        }
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Only the real desktop app builds services and windows; headless tests use the styles alone.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Services = new ServiceCollection().AddMostawsefDesktop().BuildServiceProvider();
            PageHost.Perf = Services.GetRequiredService<PerformanceMonitor>();
            PageHost.Localization = Services.GetRequiredService<LocalizationService>();
            Views.Shell.PortalShellView.PrebuildMenuPages = Services.GetRequiredService<SettingsService>().Current.PreloadViews;
            Services.GetRequiredService<ErrorReporter>().Install();
            Services.GetRequiredService<SettingsService>().ReportLoadFaultIfAny();
            Services.GetRequiredService<ThemeService>().Initialize();
            var logDirectory = Services.GetRequiredService<ErrorReporter>().LogDirectory;
            _ = Task.Run(() => LocalFileCleanup.Run(logDirectory));
            desktop.Exit += (_, _) =>
            {
                Services.GetRequiredService<SettingsService>().Flush();
                LocalFileCleanup.DeletePrints();
            };

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
