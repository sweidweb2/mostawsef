using System;
using System.Net;
using System.Net.Http.Json;
using Avalonia.Headless.XUnit;
using Mostawsef.Contracts.Logs;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.Tests.Infrastructure;
using NSubstitute;

namespace Mostawsef.Desktop.Tests.Logs;

[Collection(UiTestCollection.Name)]
public class ErrorReporterTests : UiTestBase
{
    private const string ReportPath = "/api/system-errors/report";

    [AvaloniaFact]
    public async Task Errors_are_sent_to_the_api_with_type_stack_and_context()
    {
        using var app = new TestServices();
        app.Api.On(HttpMethod.Post, "/api/system-errors/report", HttpStatusCode.Accepted);
        var reporter = Reporter(app, out _);

        await reporter.SendAsync(Thrown(new InvalidOperationException("Save failed")), ErrorLevels.Error, "Saving patient");

        var request = Assert.Single(app.Api.RequestsTo(HttpMethod.Post, "/api/system-errors/report"));
        var body = await request.Content!.ReadFromJsonAsync<ReportClientErrorRequest>(TestContext.Current.CancellationToken);
        Assert.Equal("Save failed", body!.Message);
        Assert.Equal("System.InvalidOperationException", body.ExceptionType);
        Assert.Contains(nameof(Thrown), body.StackTrace);
        Assert.Equal(ErrorLevels.Error, body.Level);
        Assert.Contains("Saving patient", body.Context);
        Assert.False(string.IsNullOrEmpty(body.OsVersion));
    }

    [AvaloniaFact]
    public async Task When_the_api_is_unreachable_errors_are_written_to_a_local_file()
    {
        using var app = new TestServices();
        app.Api.FailWithNetworkError();
        var reporter = Reporter(app, out var logDirectory);

        await reporter.SendAsync(Thrown(new InvalidOperationException("Offline crash")));

        var file = Assert.Single(Directory.GetFiles(logDirectory, "errors-*.log"));
        Assert.Contains("Offline crash", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
        Directory.Delete(logDirectory, recursive: true);
    }

    [AvaloniaFact]
    public async Task A_repeating_error_is_sent_once_per_minute_and_the_next_report_carries_the_repeat_count()
    {
        using var app = new TestServices();
        app.Api.On(HttpMethod.Post, ReportPath, HttpStatusCode.Accepted);
        var clock = new ManualClock();
        var reporter = Reporter(app, out _, clock);

        for (var i = 0; i < 5; i++)
        {
            await reporter.SendAsync(Thrown(new InvalidOperationException("Render loop")), ErrorLevels.Error, "UI");
        }

        Assert.Single(app.Api.RequestsTo(HttpMethod.Post, ReportPath));

        clock.Advance(ErrorReporter.DuplicateWindow);
        await reporter.SendAsync(Thrown(new InvalidOperationException("Render loop")), ErrorLevels.Error, "UI");

        var requests = app.Api.RequestsTo(HttpMethod.Post, ReportPath).ToList();
        Assert.Equal(2, requests.Count);
        var body = await requests[1].Content!.ReadFromJsonAsync<ReportClientErrorRequest>(TestContext.Current.CancellationToken);
        Assert.Contains("Repeated 4 more times", body!.Context);
    }

    [AvaloniaFact]
    public async Task Different_errors_beyond_the_client_limit_are_not_sent_until_the_minute_passes()
    {
        using var app = new TestServices();
        app.Api.On(HttpMethod.Post, ReportPath, HttpStatusCode.Accepted);
        var clock = new ManualClock();
        var reporter = Reporter(app, out _, clock);

        for (var i = 0; i < ErrorReporter.MaxReportsPerMinute + 5; i++)
        {
            await reporter.SendAsync(new InvalidOperationException($"Error {i}"));
        }

        Assert.Equal(ErrorReporter.MaxReportsPerMinute, app.Api.RequestsTo(HttpMethod.Post, ReportPath).Count());

        clock.Advance(TimeSpan.FromMinutes(1));
        await reporter.SendAsync(new InvalidOperationException("After the minute"));

        Assert.Equal(ErrorReporter.MaxReportsPerMinute + 1, app.Api.RequestsTo(HttpMethod.Post, ReportPath).Count());
    }

    [AvaloniaFact]
    public void A_repeating_ui_error_shows_one_toast_per_interval()
    {
        using var app = new TestServices();
        app.Api.On(HttpMethod.Post, ReportPath, HttpStatusCode.Accepted);
        var clock = new ManualClock();
        var toasts = Substitute.For<IToastService>();
        var reporter = Reporter(app, out _, clock, toasts);

        for (var i = 0; i < 5; i++)
        {
            reporter.HandleUiException(new InvalidOperationException("Binding failed"));
        }

        toasts.Received(1).Error("Something went wrong. The error was reported.");

        clock.Advance(ErrorReporter.ToastInterval);
        reporter.HandleUiException(new InvalidOperationException("Binding failed"));

        toasts.Received(2).Error("Something went wrong. The error was reported.");
    }

    [AvaloniaFact]
    public async Task When_the_server_rate_limits_reports_errors_go_to_the_local_file_for_a_minute()
    {
        using var app = new TestServices();
        app.Api.On(HttpMethod.Post, ReportPath, HttpStatusCode.TooManyRequests);
        var clock = new ManualClock();
        var reporter = Reporter(app, out var logDirectory, clock);

        await reporter.SendAsync(new InvalidOperationException("First"));
        await reporter.SendAsync(new InvalidOperationException("Second"));

        Assert.Single(app.Api.RequestsTo(HttpMethod.Post, ReportPath));
        var log = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(logDirectory, "errors-*.log")), TestContext.Current.CancellationToken);
        Assert.Contains("First", log);
        Assert.Contains("Second", log);

        clock.Advance(ErrorReporter.ServerPause);
        await reporter.SendAsync(new InvalidOperationException("Third"));

        Assert.Equal(2, app.Api.RequestsTo(HttpMethod.Post, ReportPath).Count());
        Directory.Delete(logDirectory, recursive: true);
    }

    [AvaloniaFact]
    public async Task An_error_storm_sends_at_most_one_report_at_a_time()
    {
        using var app = new TestServices();
        var inFlight = 0;
        var peak = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Api.On(HttpMethod.Post, ReportPath, async (_, _) =>
        {
            var now = Interlocked.Increment(ref inFlight);
            Interlocked.Exchange(ref peak, Math.Max(peak, now));
            await gate.Task;
            Interlocked.Decrement(ref inFlight);
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        });
        var reporter = Reporter(app, out _);

        var sends = Enumerable.Range(0, 5)
            .Select(i => reporter.SendAsync(new InvalidOperationException($"Storm {i}")))
            .ToArray();
        await UiWait.UntilAsync(() => Volatile.Read(ref inFlight) == 1, pumpDispatcher: false);

        Assert.Equal(1, Volatile.Read(ref inFlight));
        gate.SetResult();
        await Task.WhenAll(sends);

        Assert.Equal(1, peak);
        Assert.Equal(5, app.Api.RequestsTo(HttpMethod.Post, ReportPath).Count());
    }

    private static ErrorReporter Reporter(TestServices app, out string logDirectory, TimeProvider? clock = null, IToastService? toasts = null)
    {
        logDirectory = Path.Combine(Path.GetTempPath(), "mostawsef-tests", Guid.NewGuid().ToString("N"));
        return new ErrorReporter(
            new Lazy<LogsApi>(app.Get<LogsApi>),
            new Lazy<NavigationService>(() => app.Navigation),
            new Lazy<IToastService>(() => toasts ?? app.Get<IToastService>()),
            new Lazy<LocalizationService>(() => app.Localization))
        {
            LogDirectory = logDirectory,
            Clock = clock ?? TimeProvider.System,
        };
    }

    private static Exception Thrown(Exception exception)
    {
        try
        {
            throw exception;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
