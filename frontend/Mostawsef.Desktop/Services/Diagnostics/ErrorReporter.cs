using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using Mostawsef.Contracts.Logs;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Ui;

namespace Mostawsef.Desktop.Services.Diagnostics;

/// <summary>
/// Desktop side of the system error engine. Sends errors to the API (system_errors table);
/// if the API is unreachable, appends them to %AppData%\Mostawsef\logs\errors-yyyyMMdd.log.
/// Install() hooks every global unhandled-exception source; call Report() from catch blocks you handle yourself.
/// </summary>
public class ErrorReporter(LogsApi logsApi, NavigationService navigation, IToastService toasts)
{
    private static readonly string AppVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

    public string LogDirectory { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mostawsef", "logs");

    public void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                // The process is going down: write locally first, then try the API.
                WriteLocal(ex, ErrorLevels.Critical, "AppDomain.UnhandledException");
                _ = SendAsync(ex, ErrorLevels.Critical, "AppDomain.UnhandledException");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            Report(e.Exception, ErrorLevels.Error, "TaskScheduler.UnobservedTaskException");
        };

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            Report(e.Exception, ErrorLevels.Error, "UI");
            toasts.Error(LocalizationService.Instance.Get("Error.Unexpected"));
        };
    }

    /// <summary>Fire-and-forget. Never throws.</summary>
    public void Report(Exception exception, string level = ErrorLevels.Error, string? context = null) =>
        _ = SendAsync(exception, level, context);

    public async Task SendAsync(Exception exception, string level = ErrorLevels.Error, string? context = null)
    {
        try
        {
            var request = new ReportClientErrorRequest(
                exception.Message,
                exception.GetType().FullName,
                exception.ToString(),
                level,
                Context(context),
                AppVersion,
                RuntimeInformation.OSDescription);

            var result = await logsApi.ReportErrorAsync(request);
            if (!result.IsSuccess)
            {
                WriteLocal(exception, level, context);
            }
        }
        catch (Exception)
        {
            WriteLocal(exception, level, context);
        }
    }

    private string Context(string? context)
    {
        var page = navigation.CurrentPage?.GetType().Name;
        return string.Join(" | ", new[] { context, page is null ? null : $"Page: {page}" }.Where(s => !string.IsNullOrEmpty(s)));
    }

    private void WriteLocal(Exception exception, string level, string? context)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var file = Path.Combine(LogDirectory, $"errors-{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(file, $"[{DateTime.Now:O}] {level} {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Last resort: nothing else we can do.
        }
    }
}
