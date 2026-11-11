using System.Diagnostics;
using System.Net;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Admin;

namespace Mostawsef.Desktop.Tests.Diagnostics;

[Collection(UiTestCollection.Name)]
public class PerformanceMonitorTests : UiTestBase
{
    [AvaloniaFact]
    public void Record_adds_newest_first_and_caps_the_list()
    {
        using var app = new TestServices();
        var perf = app.Get<PerformanceMonitor>();
        perf.IsEnabled = true;
        perf.WriteToFile = false;

        for (var i = 0; i < PerformanceMonitor.MaxEntries + 5; i++)
        {
            perf.Record("Api", $"call-{i}", TimeSpan.FromMilliseconds(i));
        }

        Assert.Equal(PerformanceMonitor.MaxEntries, perf.Recent.Count);
        Assert.Equal($"call-{PerformanceMonitor.MaxEntries + 4}", perf.Recent[0].Name);
        Assert.True(perf.HasEntries);
    }

    [AvaloniaFact]
    public void Disabled_monitor_ignores_records()
    {
        using var app = new TestServices();
        var perf = app.Get<PerformanceMonitor>();
        perf.IsEnabled = false;
        perf.WriteToFile = false;

        perf.Record("Api", "ignored", TimeSpan.FromMilliseconds(10));

        Assert.Empty(perf.Recent);
        Assert.False(perf.HasEntries);
    }

    [AvaloniaFact]
    public void Measure_records_on_dispose()
    {
        using var app = new TestServices();
        var perf = app.Get<PerformanceMonitor>();
        perf.IsEnabled = true;
        perf.WriteToFile = false;

        using (perf.Measure("Page", "PatientsViewModel"))
        {
        }

        var entry = Assert.Single(perf.Recent);
        Assert.Equal("Page", entry.Category);
        Assert.Equal("PatientsViewModel", entry.Name);
    }

    [AvaloniaFact]
    public void Slow_and_warn_thresholds_match_the_overlay_colors()
    {
        Assert.True(new PerfEntry
        {
            At = DateTimeOffset.Now,
            Category = "Api",
            Name = "x",
            Duration = PerformanceMonitor.SlowThreshold,
        }.IsSlow);

        Assert.True(new PerfEntry
        {
            At = DateTimeOffset.Now,
            Category = "Api",
            Name = "x",
            Duration = PerformanceMonitor.WarnThreshold,
        }.IsWarn);

        Assert.False(new PerfEntry
        {
            At = DateTimeOffset.Now,
            Category = "Api",
            Name = "x",
            Duration = TimeSpan.FromMilliseconds(50),
        }.IsWarn);
    }

    [AvaloniaFact]
    public async Task Api_client_records_method_path_and_status()
    {
        using var app = new TestServices();
        var perf = app.Get<PerformanceMonitor>();
        perf.IsEnabled = true;
        perf.WriteToFile = false;
        app.Api.RespondWithStatus(HttpStatusCode.NoContent);

        await app.Get<ApiClient>().DeleteAsync("api/users/1?x=1", TestContext.Current.CancellationToken);

        var entry = Assert.Single(perf.Recent);
        Assert.Equal("Api", entry.Category);
        Assert.Equal("DELETE api/users/1", entry.Name);
        Assert.Contains("204", entry.Detail);
    }

    [AvaloniaFact]
    public async Task Health_checks_are_not_recorded()
    {
        using var app = new TestServices();
        var perf = app.Get<PerformanceMonitor>();
        perf.IsEnabled = true;
        perf.WriteToFile = false;
        app.Api.Respond = _ => FakeApiHandler.Json(new { status = "ok" });

        await app.Get<ApiClient>().GetAsync<object>("api/health", TestContext.Current.CancellationToken);

        Assert.Empty(perf.Recent);
    }

    [AvaloniaFact]
    public void Overlay_toggle_and_clear_commands_work()
    {
        using var app = new TestServices();
        var perf = app.Get<PerformanceMonitor>();
        perf.IsEnabled = true;
        perf.WriteToFile = false;
        perf.IsOverlayVisible = true;
        perf.Record("Api", "one", TimeSpan.FromMilliseconds(1));
        Dispatcher.UIThread.RunJobs();

        Assert.True(perf.ToggleOverlayCommand.CanExecute(null));
        perf.ToggleOverlayCommand.Execute(null);
        Assert.False(perf.IsOverlayVisible);
        Assert.Empty(perf.Entries);

        perf.ClearCommand.Execute(null);
        Assert.Empty(perf.Recent);
        Assert.False(perf.HasEntries);
    }

    [AvaloniaFact]
    public void Hidden_overlay_does_not_fill_the_bound_collection()
    {
        using var app = new TestServices();
        var perf = app.Get<PerformanceMonitor>();
        perf.IsEnabled = true;
        perf.WriteToFile = false;
        perf.IsOverlayVisible = false;

        perf.Record("Api", "file-only", TimeSpan.FromMilliseconds(1));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(perf.Recent);
        Assert.Empty(perf.Entries);
    }

    [AvaloniaFact]
    public void Showing_the_overlay_flushes_recent_entries_when_idle()
    {
        using var app = new TestServices();
        var perf = app.Get<PerformanceMonitor>();
        perf.IsEnabled = true;
        perf.WriteToFile = false;
        perf.IsOverlayVisible = false;
        perf.Record("Api", "later", TimeSpan.FromMilliseconds(1));

        perf.IsOverlayVisible = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Single(perf.Entries);
        Assert.Equal("later", perf.Entries[0].Name);
    }

    [AvaloniaFact]
    public void Overlay_shows_the_performance_title_when_open()
    {
        using var app = new TestServices();
        var perf = app.Get<PerformanceMonitor>();
        perf.IsOverlayVisible = true;
        var window = app.Show(new Mostawsef.Desktop.Views.Dev.PerfOverlay { DataContext = perf });

        var texts = TestServices.Texts(window);
        Assert.Contains("Performance", texts);
        Assert.Contains("Ctrl+Shift+P", texts);
    }

    [AvaloniaFact]
    public void Machine_snapshot_includes_cpu_and_working_set()
    {
        using var app = new TestServices();
        var first = app.Get<PerformanceMonitor>().MachineSnapshot();
        var second = app.Get<PerformanceMonitor>().MachineSnapshot();

        Assert.Contains("cpu=", first);
        Assert.Contains("ws=", second);
        Assert.Contains("gc0=", second);
    }

    [AvaloniaFact]
    public void ProbeUiAfter_records_layout_and_dispatcher_lag()
    {
        using var app = new TestServices();
        var perf = app.Get<PerformanceMonitor>();
        perf.IsEnabled = true;
        perf.WriteToFile = false;
        var sw = Stopwatch.StartNew();

        perf.ProbeUiAfter("PatientsViewModel", sw);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(perf.Recent, e => e.Category == "Ui" && e.Name == "PatientsViewModel" && e.Detail!.Contains("layout"));
        Assert.Contains(perf.Recent, e => e.Category == "Ui" && e.Name == "dispatcher-lag" && e.Detail!.Contains("PatientsViewModel"));
    }

    [AvaloniaFact]
    public void PageHost_records_view_created_then_reused()
    {
        using var app = new TestServices();
        var perf = app.Get<PerformanceMonitor>();
        perf.IsEnabled = true;
        perf.WriteToFile = false;
        var page = app.Get<PatientsViewModel>();
        var host = new PageHost();
        app.Show(host);

        host.Page = page;
        host.Page = null;
        host.Page = page;

        Assert.Contains(perf.Recent, e => e.Category == "View" && e.Name == nameof(PatientsViewModel) && e.Detail == "created");
        Assert.Contains(perf.Recent, e => e.Category == "View" && e.Name == nameof(PatientsViewModel) && e.Detail == "reused");
        Assert.Contains(perf.Recent, e => e.Category == "View" && e.Name == nameof(PatientsViewModel) && e.Detail == "show");
        Assert.Contains(perf.Recent, e => e.Category == "View" && e.Name == nameof(PatientsViewModel) && e.Detail == "hide");
    }
}
