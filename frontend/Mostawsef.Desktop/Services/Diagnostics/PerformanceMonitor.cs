using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.Services.Diagnostics;

/// <summary>
/// App-wide speed debugger: times API calls, page loads, view build, UI-thread lag and printing.
/// Always appends to %AppData%\Mostawsef\logs\perf-yyyyMMdd.log when enabled.
/// The overlay (Ctrl+Shift+P) is off by default and refreshes only when visible, deferred to the
/// background dispatcher priority, so it does not allocate/layout during navigation.
/// </summary>
public partial class PerformanceMonitor : ObservableObject
{
    public const int MaxEntries = 60;

    /// <summary>Calls at or above this duration are marked warning (amber).</summary>
    public static readonly TimeSpan WarnThreshold = TimeSpan.FromMilliseconds(200);

    /// <summary>Calls at or above this duration are marked slow (red).</summary>
    public static readonly TimeSpan SlowThreshold = TimeSpan.FromMilliseconds(500);

    private readonly Lock _gate = new();
    private readonly List<PerfEntry> _recent = [];
    private readonly SemaphoreSlim _fileGate = new(1, 1);
    private readonly Process _process = Process.GetCurrentProcess();
    private TimeSpan _lastCpu = TimeSpan.Zero;
    private DateTimeOffset _lastCpuAt;
    private bool _overlayRefreshPending;

    public PerformanceMonitor()
    {
#if DEBUG
        IsEnabled = true;
#else
        IsEnabled = string.Equals(
            Environment.GetEnvironmentVariable("MOSTAWSEF_PERF"),
            "1",
            StringComparison.Ordinal);
#endif
        // Overlay off by default: live ItemsControl updates during Nav were amplifying GC/UI stalls.
        IsOverlayVisible = false;
    }

    /// <summary>When false, Record/Measure are no-ops (no memory, no file, no overlay updates).</summary>
    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    /// <summary>Floating panel on MainWindow. Independent of <see cref="IsEnabled"/> so you can hide it while still logging to the file.</summary>
    [ObservableProperty]
    public partial bool IsOverlayVisible { get; set; }

    public string LogDirectory { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mostawsef", "logs");

    /// <summary>Set false in tests that only care about in-memory entries.</summary>
    public bool WriteToFile { get; set; } = true;

    public TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <summary>Bound by the overlay only. Empty while the overlay is hidden.</summary>
    public ListPageItems<PerfEntry> Entries { get; } = [];

    /// <summary>In-memory ring (newest first), updated even when the overlay is hidden. Prefer this in tests.</summary>
    public IReadOnlyList<PerfEntry> Recent
    {
        get
        {
            lock (_gate)
            {
                return _recent.ToList();
            }
        }
    }

    public bool HasEntries
    {
        get
        {
            lock (_gate)
            {
                return _recent.Count > 0;
            }
        }
    }

    public string LogFilePath => Path.Combine(LogDirectory, $"perf-{Clock.GetLocalNow():yyyyMMdd}.log");

    partial void OnIsOverlayVisibleChanged(bool value)
    {
        if (value)
        {
            ScheduleOverlayRefresh();
        }
        else
        {
            Entries.ReplaceAll([]);
        }
    }

    [RelayCommand]
    private void ToggleOverlay() => IsOverlayVisible = !IsOverlayVisible;

    [RelayCommand]
    private void Clear()
    {
        var hadEntries = false;
        lock (_gate)
        {
            hadEntries = _recent.Count > 0;
            _recent.Clear();
        }

        Entries.ReplaceAll([]);
        if (hadEntries)
        {
            OnPropertyChanged(nameof(HasEntries));
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = LogDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception)
        {
            // Debug tool: never disturb the user for a folder open failure.
        }
    }

    /// <summary>Starts a timed scope; Dispose records it. No-op when disabled.</summary>
    public PerfScope Measure(string category, string name, string? detail = null) =>
        IsEnabled ? new PerfScope(this, category, name, detail) : default;

    /// <summary>
    /// After a navigation sets the page: posts callbacks to see when layout finishes and when the UI
    /// thread is idle again. If <c>Ui / dispatcher-lag</c> is high while <c>Api</c>/<c>Page</c> are low,
    /// the delay is the UI thread (view build, layout, or a busy CPU), not the server.
    /// </summary>
    public void ProbeUiAfter(string pageName, Stopwatch sinceNavStart)
    {
        if (!IsEnabled)
        {
            return;
        }

        var machine = MachineSnapshot();
        Dispatcher.UIThread.Post(
            () => Record("Ui", pageName, sinceNavStart.Elapsed, $"layout {machine}"),
            DispatcherPriority.Loaded);

        var lag = Stopwatch.StartNew();
        Dispatcher.UIThread.Post(
            () => Record("Ui", "dispatcher-lag", lag.Elapsed, $"idle after {pageName}; {MachineSnapshot()}"),
            DispatcherPriority.Background);
    }

    /// <summary>
    /// Process CPU since the last sample, working set and GC counts. Cheap enough to attach to Nav/Ui lines.
    /// High cpu% with small Api/Page times points at a loaded machine or UI work, not the API.
    /// </summary>
    public string MachineSnapshot()
    {
        try
        {
            _process.Refresh();
            var now = Clock.GetUtcNow();
            var cpu = _process.TotalProcessorTime;
            double cpuPct;
            lock (_gate)
            {
                cpuPct = 0.0;
                if (_lastCpuAt != default)
                {
                    var wallMs = (now - _lastCpuAt).TotalMilliseconds;
                    if (wallMs > 1)
                    {
                        cpuPct = 100.0 * (cpu - _lastCpu).TotalMilliseconds
                            / (wallMs * Math.Max(1, Environment.ProcessorCount));
                    }
                }

                _lastCpu = cpu;
                _lastCpuAt = now;
            }

            var wsMb = _process.WorkingSet64 / (1024d * 1024d);
            return $"cpu={cpuPct:F0}% ws={wsMb:F0}MB gc0={GC.CollectionCount(0)} gc1={GC.CollectionCount(1)}";
        }
        catch (Exception)
        {
            return "cpu=?";
        }
    }

    public void Record(string category, string name, TimeSpan duration, string? detail = null)
    {
        if (!IsEnabled)
        {
            return;
        }

        var entry = new PerfEntry
        {
            At = Clock.GetLocalNow(),
            Category = category,
            Name = name,
            Duration = duration,
            Detail = detail,
        };

        var firstEntry = false;
        lock (_gate)
        {
            firstEntry = _recent.Count == 0;
            _recent.Insert(0, entry);
            while (_recent.Count > MaxEntries)
            {
                _recent.RemoveAt(_recent.Count - 1);
            }
        }

        if (firstEntry)
        {
            OnPropertyChanged(nameof(HasEntries));
        }

        if (IsOverlayVisible)
        {
            ScheduleOverlayRefresh();
        }

        if (WriteToFile)
        {
            _ = AppendFileAsync(entry);
        }
    }

    /// <summary>Pushes <see cref="_recent"/> into <see cref="Entries"/> (tests may call after enabling the overlay).</summary>
    public void FlushOverlay()
    {
        List<PerfEntry> snapshot;
        lock (_gate)
        {
            snapshot = _recent.ToList();
        }

        Entries.ReplaceAll(snapshot);
    }

    private void ScheduleOverlayRefresh()
    {
        if (!IsOverlayVisible)
        {
            return;
        }

        // No Avalonia dispatcher (unit-style callers): refresh inline.
        if (Avalonia.Application.Current is null)
        {
            FlushOverlay();
            return;
        }

        lock (_gate)
        {
            if (_overlayRefreshPending)
            {
                return;
            }

            _overlayRefreshPending = true;
        }

        // Never mutate the ItemsControl during PageHost/Nav sync work — wait until the UI thread is idle.
        Dispatcher.UIThread.Post(
            () =>
            {
                lock (_gate)
                {
                    _overlayRefreshPending = false;
                }

                if (IsOverlayVisible)
                {
                    FlushOverlay();
                }
            },
            DispatcherPriority.Background);
    }

    private async Task AppendFileAsync(PerfEntry entry)
    {
        try
        {
            await _fileGate.WaitAsync().ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(LogDirectory);
                await File.AppendAllTextAsync(LogFilePath, entry.FileLine + Environment.NewLine).ConfigureAwait(false);
            }
            finally
            {
                _fileGate.Release();
            }
        }
        catch (Exception)
        {
            // Debug tool: never throw into the timed action.
        }
    }
}

/// <summary>One timed event shown in the overlay and written to the perf log.</summary>
public sealed class PerfEntry
{
    public required DateTimeOffset At { get; init; }

    public required string Category { get; init; }

    public required string Name { get; init; }

    public required TimeSpan Duration { get; init; }

    public string? Detail { get; init; }

    public bool IsSlow => Duration >= PerformanceMonitor.SlowThreshold;

    public bool IsWarn => !IsSlow && Duration >= PerformanceMonitor.WarnThreshold;

    public string Line
    {
        get
        {
            var ms = $"{Duration.TotalMilliseconds:F0}ms";
            return Detail is { Length: > 0 }
                ? $"{At:HH:mm:ss.fff}  {ms,6}  [{Category}] {Name}  {Detail}"
                : $"{At:HH:mm:ss.fff}  {ms,6}  [{Category}] {Name}";
        }
    }

    public string FileLine
    {
        get
        {
            var ms = $"{Duration.TotalMilliseconds:F1}ms";
            return Detail is { Length: > 0 }
                ? $"{At:yyyy-MM-dd HH:mm:ss.fff} | {ms} | {Category} | {Name} | {Detail}"
                : $"{At:yyyy-MM-dd HH:mm:ss.fff} | {ms} | {Category} | {Name}";
        }
    }
}

/// <summary>Stopwatch scope created by <see cref="PerformanceMonitor.Measure"/>. Default (disabled) is a no-op.</summary>
public struct PerfScope : IDisposable
{
    private readonly PerformanceMonitor? _monitor;
    private readonly string _category;
    private readonly string _name;
    private string? _detail;
    private readonly Stopwatch _sw;

    public PerfScope(PerformanceMonitor monitor, string category, string name, string? detail)
    {
        _monitor = monitor;
        _category = category;
        _name = name;
        _detail = detail;
        _sw = Stopwatch.StartNew();
    }

    public void SetDetail(string? detail) => _detail = detail;

    public void Dispose()
    {
        if (_monitor is null)
        {
            return;
        }

        _sw.Stop();
        _monitor.Record(_category, _name, _sw.Elapsed, _detail);
    }
}
