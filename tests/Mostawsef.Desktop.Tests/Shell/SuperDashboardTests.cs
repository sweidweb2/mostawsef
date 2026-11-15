using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Dashboards;
using Mostawsef.Contracts.Maintenance;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Shell;
using Mostawsef.Desktop.ViewModels.Super;
using static Mostawsef.Desktop.Tests.Orders.OrderPages;

namespace Mostawsef.Desktop.Tests.Shell;

[Collection(UiTestCollection.Name)]
public class SuperDashboardTests : UiTestBase
{
    private const string DashboardPath = "/api/system/dashboard";

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    private static DateOnly FirstOfMonth => new(Today.Year, Today.Month, 1);

    [AvaloniaFact]
    public void Only_super_users_get_the_system_dashboard()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        Assert.False(app.Navigation.CanAccess(typeof(SuperDashboardViewModel)));

        app.SignInAs(Roles.Super);
        Assert.True(app.Navigation.CanAccess(typeof(SuperDashboardViewModel)));
    }

    [AvaloniaFact]
    public async Task Shows_this_months_stat_cards_status_bars_and_five_graphs_without_calling_admin_endpoints()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        var dashboard = Sample();
        app.Api.OnGet(DashboardPath, dashboard);

        var page = Assert.IsType<SuperDashboardViewModel>(app.Navigation.CreatePage(typeof(SuperDashboardViewModel)));
        var window = app.Show(page);
        await page.ActivateAsync();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.Equal(
            $"?from={FirstOfMonth:yyyy-MM-dd}&to={Today:yyyy-MM-dd}",
            Assert.Single(app.Api.RequestsTo(HttpMethod.Get, DashboardPath)).RequestUri!.Query);
        Assert.DoesNotContain(app.Api.Requests, r => r.RequestUri!.AbsolutePath.StartsWith("/api/orders", StringComparison.Ordinal));

        Assert.Equal("1,234.50", Named<StatCard>(window, "RevenueCard").Value);
        Assert.Equal("EGP", Named<StatCard>(window, "RevenueCard").Caption);
        Assert.Equal("300.00", Named<StatCard>(window, "OutstandingCard").Value);
        Assert.Equal("10", Named<StatCard>(window, "OrdersCard").Value);
        Assert.Equal("3 active", Named<StatCard>(window, "UsersCard").Caption);
        Assert.Equal("Current total", Named<StatCard>(window, "LabTestsCard").Caption);
        Assert.Equal("4", Named<StatCard>(window, "ErrorsCard").Value);

        Assert.Equal((5.0, 5.0), (Named<ProgressBar>(window, "PaidBar").Maximum, Named<ProgressBar>(window, "PaidBar").Value));
        Assert.Equal("50%", page.PaidPercent);
        Assert.Equal("10%", page.RefundedPercent);

        foreach (var name in new[] { "RevenueChart", "OrdersChart", "PatientsChart", "ActivityChart", "ErrorsChart" })
        {
            var chart = Named<LineChart>(window, name);
            Assert.True(chart.IsEffectivelyVisible, name);
            Assert.Equal(dashboard.Points.Count, chart.Points!.Count);
        }

        // Revenue is drawn in whole currency units: 250.75 rounds to 251.
        Assert.Equal(251, page.RevenueOverTime![^1].Value);
        Assert.True(Named<CalendarDatePicker>(window, "DayPicker") is { IsEffectivelyVisible: false });
    }

    [AvaloniaFact]
    public async Task A_year_is_drawn_per_month_after_choosing_it()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api.On(HttpMethod.Get, DashboardPath, request => FakeApiHandler.Json(
            request.RequestUri!.Query.StartsWith($"?from={Today.Year}-01-01", StringComparison.Ordinal) ? Sample(DashboardBuckets.Month) : Sample()));
        var page = app.Get<SuperDashboardViewModel>();
        await page.ActivateAsync();

        page.Filter.SelectedPeriod = DashboardPeriodFilter.PeriodOptions.Single(o => o.Period == DashboardPeriod.Year);
        await WaitUntil(() => page.Unit == ChartUnit.Month && !page.IsLoading);

        Assert.Equal($"?from={Today.Year}-01-01&to={Today:yyyy-MM-dd}", app.Api.Requests[^1].RequestUri!.Query);
        Assert.All(page.OrdersOverTime!, p => Assert.Equal(ChartUnit.Month, p.Unit));
        Assert.Equal("Dashboard.Bucket.Month", page.BucketKey);
    }

    [AvaloniaFact]
    public async Task Coming_back_soon_reuses_the_figures_and_refresh_always_reloads()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api.OnGet(DashboardPath, Sample());
        var page = app.Get<SuperDashboardViewModel>();

        await page.ActivateAsync();
        page.Deactivate();
        await page.ActivateAsync();
        Assert.Single(app.Api.RequestsTo(HttpMethod.Get, DashboardPath));

        await page.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(2, app.Api.RequestsTo(HttpMethod.Get, DashboardPath).Count());
    }

    [AvaloniaFact]
    public async Task A_failed_load_is_tried_again_when_the_page_is_shown_again()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api.FailWithNetworkError();
        var page = app.Get<SuperDashboardViewModel>();
        await page.ActivateAsync();

        app.Api.OnGet(DashboardPath, Sample());
        await page.ActivateAsync();

        Assert.NotNull(page.Dashboard);
    }

    [AvaloniaFact]
    public async Task A_failed_load_shows_the_error_state()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);
        app.Api.FailWithNetworkError();

        var page = app.Get<SuperDashboardViewModel>();
        var window = app.Show(page);
        await page.ActivateAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.True(page.HasLoadError);
        Assert.True(window.GetVisualDescendants().OfType<ErrorState>().Single().IsEffectivelyVisible);
    }

    /// <summary>This month per day, or this year per month.</summary>
    private static SuperDashboardDto Sample(string bucket = DashboardBuckets.Day)
    {
        var starts = bucket == DashboardBuckets.Month
            ? Enumerable.Range(0, Today.Month).Select(m => new DateTime(Today.Year, m + 1, 1)).ToList()
            : Enumerable.Range(0, Today.Day).Select(d => FirstOfMonth.AddDays(d).ToDateTime(TimeOnly.MinValue)).ToList();
        var points = starts
            .Select((start, i) => new DashboardPointDto(start, i % 3, i % 2, i == starts.Count - 1 ? 25_075 : 1000 * i, i * 4, i % 4))
            .ToList();
        var from = bucket == DashboardBuckets.Month ? new DateOnly(Today.Year, 1, 1) : FirstOfMonth;
        return new SuperDashboardDto(
            "EGP",
            new DashboardRangeDto(from, Today, Today.AddYears(-2), Today, bucket),
            Users: 4, ActiveUsers: 3, LabTests: 40,
            Orders: 10, Patients: 25, Revenue: 123_450, Outstanding: 30_000, Activity: 116, Errors: 4,
            [
                new(OrderStatuses.Pending, 1), new(OrderStatuses.PartiallyPaid, 2), new(OrderStatuses.Paid, 5),
                new(OrderStatuses.Cancelled, 1), new(OrderStatuses.Refunded, 1),
            ],
            points);
    }
}
