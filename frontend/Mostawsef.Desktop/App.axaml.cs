using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Diagnostics;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Updates;
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
            var args = Environment.GetCommandLineArgs();
            if (UpdateApplicator.TryParseArgs(args, out var setupPath, out var waitPid))
            {
                RunApplyUpdate(desktop, setupPath, waitPid);
                base.OnFrameworkInitializationCompleted();
                return;
            }

            if (UpdateApplicator.IsUpdateLockHeld())
            {
                RunWaitForUpdate(desktop);
                base.OnFrameworkInitializationCompleted();
                return;
            }

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

    private static void RunApplyUpdate(IClassicDesktopStyleApplicationLifetime desktop, string setupPath, int? waitPid)
    {
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var host = new UpdateHost();
        var waitWindow = CreatePleaseWaitWindow();
        desktop.MainWindow = waitWindow;
        waitWindow.Show();

        _ = Task.Run(() =>
        {
            try
            {
                UpdateApplicator.Apply(setupPath, host.DesktopExePath, waitPid);
            }
            finally
            {
                Dispatcher.UIThread.Post(() => desktop.Shutdown());
            }
        });
    }

    private static void RunWaitForUpdate(IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var waitWindow = CreatePleaseWaitWindow();
        desktop.MainWindow = waitWindow;
        waitWindow.Show();

        _ = Task.Run(() =>
        {
            UpdateApplicator.WaitForUpdateLock(TimeSpan.FromMinutes(30));
            Dispatcher.UIThread.Post(() =>
            {
                // Relaunch a normal instance so DI / login start cleanly after Setup.
                try
                {
                    var exe = Environment.ProcessPath
                        ?? System.IO.Path.Combine(AppContext.BaseDirectory, "Mostawsef.Desktop.exe");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exe,
                        UseShellExecute = true,
                        WorkingDirectory = AppContext.BaseDirectory,
                    });
                }
                catch
                {
                    // User can open the shortcut manually.
                }

                desktop.Shutdown();
            });
        });
    }

    private static Window CreatePleaseWaitWindow()
    {
        // Minimal UI without DI / localization services (apply-update path has no Services yet).
        var text = new TextBlock
        {
            Text = "Updating Mostawsef… Please wait.",
            Margin = new Thickness(24),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        };
        return new Window
        {
            Title = "Mostawsef",
            Width = 420,
            Height = 140,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = text,
        };
    }
}
