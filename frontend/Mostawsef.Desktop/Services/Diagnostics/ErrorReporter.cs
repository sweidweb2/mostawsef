using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
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
/// A repeating error is sent once per DuplicateWindow (the next report carries the repeat count),
/// at most MaxReportsPerMinute errors are admitted, at most MaxConcurrentSends POSTs run at once,
/// and the "unexpected error" toast shows at most once per ToastInterval.
/// </summary>
public class ErrorReporter(
    Lazy<LogsApi> logsApi,
    Lazy<NavigationService> navigation,
    Lazy<IToastService> toasts,
    Lazy<LocalizationService> localization)
{
    public const int MaxReportsPerMinute = 10;

    /// <summary>One in-flight report POST at a time so an error storm cannot open MaxReportsPerMinute connections.</summary>
    public const int MaxConcurrentSends = 1;

    public static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(1);

    public static readonly TimeSpan ToastInterval = TimeSpan.FromSeconds(10);

    /// <summary>How long reports go to the local file only after the server answers 429.</summary>
    public static readonly TimeSpan ServerPause = TimeSpan.FromMinutes(1);

    private const int MaxTrackedErrors = 200;
    private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(1);
    private static readonly string AppVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

    private readonly Lock _gate = new();
    private readonly Dictionary<string, Occurrence> _recent = [];
    private readonly Queue<DateTimeOffset> _admitted = new();
    private readonly SemaphoreSlim _sendGate = new(MaxConcurrentSends, MaxConcurrentSends);
    private readonly SemaphoreSlim _fileGate = new(1, 1);
    private DateTimeOffset _serverPausedUntil;
    private DateTimeOffset? _lastToastAt;

    public string LogDirectory { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mostawsef", "logs");

    public TimeProvider Clock { get; init; } = TimeProvider.System;

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
            HandleUiException(e.Exception);
        };
    }

    internal void HandleUiException(Exception exception)
    {
        Report(exception, ErrorLevels.Error, "UI");
        if (TryClaimToast())
        {
            toasts.Value.Error(localization.Value.Get("Error.Unexpected"));
        }
    }

    /// <summary>Fire-and-forget. Never throws.</summary>
    public void Report(Exception exception, string level = ErrorLevels.Error, string? context = null) =>
        _ = SendAsync(exception, level, context);

    public async Task SendAsync(Exception exception, string level = ErrorLevels.Error, string? context = null)
    {
        if (!TryAdmit($"{level}|{exception.GetType().FullName}|{exception.Message}|{context}", out var repeats, out var serverPaused))
        {
            return;
        }

        await _sendGate.WaitAsync();
        try
        {
            var fullContext = context;
            try
            {
                fullContext = Context(context, repeats);
                if (!serverPaused)
                {
                    var request = new ReportClientErrorRequest(
                        exception.Message,
                        exception.GetType().FullName,
                        exception.ToString(),
                        level,
                        fullContext,
                        AppVersion,
                        RuntimeInformation.OSDescription);

                    var result = await logsApi.Value.ReportErrorAsync(request);
                    if (result.IsSuccess)
                    {
                        return;
                    }

                    if (result.Error == ApiError.TooManyRequests)
                    {
                        PauseServerReports();
                    }
                }
            }
            catch (Exception)
            {
                // Fall through to the local file.
            }

            await WriteLocalAsync(exception, level, fullContext);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private bool TryAdmit(string key, out int repeats, out bool serverPaused)
    {
        var now = Clock.GetUtcNow();
        lock (_gate)
        {
            repeats = 0;
            serverPaused = now < _serverPausedUntil;

            if (_recent.TryGetValue(key, out var seen) && now - seen.SentAt < DuplicateWindow)
            {
                seen.Repeats++;
                return false;
            }

            while (_admitted.Count > 0 && now - _admitted.Peek() >= RateWindow)
            {
                _admitted.Dequeue();
            }

            if (_admitted.Count >= MaxReportsPerMinute)
            {
                return false;
            }

            if (_recent.Count >= MaxTrackedErrors)
            {
                foreach (var expired in _recent.Where(r => now - r.Value.SentAt >= DuplicateWindow).Select(r => r.Key).ToList())
                {
                    _recent.Remove(expired);
                }
            }

            _admitted.Enqueue(now);
            repeats = seen?.Repeats ?? 0;
            _recent[key] = new Occurrence(now);
            return true;
        }
    }

    private void PauseServerReports()
    {
        lock (_gate)
        {
            _serverPausedUntil = Clock.GetUtcNow() + ServerPause;
        }
    }

    private bool TryClaimToast()
    {
        var now = Clock.GetUtcNow();
        lock (_gate)
        {
            if (_lastToastAt is { } last && now - last < ToastInterval)
            {
                return false;
            }

            _lastToastAt = now;
            return true;
        }
    }

    private string Context(string? context, int repeats)
    {
        var page = navigation.Value.CurrentPage?.GetType().Name;
        return string.Join(" | ", new[]
        {
            context,
            page is null ? null : $"Page: {page}",
            repeats == 0 ? null : $"Repeated {repeats} more times since the last report",
        }.Where(s => !string.IsNullOrEmpty(s)));
    }

    private async Task WriteLocalAsync(Exception exception, string level, string? context)
    {
        try
        {
            await _fileGate.WaitAsync();
            try
            {
                Directory.CreateDirectory(LogDirectory);
                await File.AppendAllTextAsync(LogFile(), LogEntry(exception, level, context));
            }
            finally
            {
                _fileGate.Release();
            }
        }
        catch (Exception)
        {
            // Last resort: nothing else we can do.
        }
    }

    /// <summary>Synchronous, for the process-crash handler only.</summary>
    private void WriteLocal(Exception exception, string level, string? context)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(LogFile(), LogEntry(exception, level, context));
        }
        catch (Exception)
        {
            // Last resort: nothing else we can do.
        }
    }

    private string LogFile() => Path.Combine(LogDirectory, $"errors-{Clock.GetLocalNow():yyyyMMdd}.log");

    private string LogEntry(Exception exception, string level, string? context) =>
        $"[{Clock.GetLocalNow():O}] {level} {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";

    private sealed class Occurrence(DateTimeOffset sentAt)
    {
        public DateTimeOffset SentAt { get; } = sentAt;

        public int Repeats { get; set; }
    }
}
