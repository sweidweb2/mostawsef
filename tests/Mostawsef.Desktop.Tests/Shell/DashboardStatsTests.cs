using System.Net.Http;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Dashboards;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Shell;
using static Mostawsef.Desktop.Tests.Orders.OrderPages;

namespace Mostawsef.Desktop.Tests.Shell;

[Collection(UiTestCollection.Name)]
public class DashboardStatsTests : UiTestBase
{
    private const string DashboardPath = "/api/orders/dashboard";

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    private static DateOnly FirstOfMonth => new(Today.Year, Today.Month, 1);

    /// <summary>This month per day (the default period), or today per hour.</summary>
    private static OrderDashboardDto Sample(OrderStatsDto? stats = null, IReadOnlyList<TopTestDto>? top = null, string bucket = DashboardBuckets.Day)
    {
        var points = bucket == DashboardBuckets.Hour
            ? Enumerable.Range(0, 24).Select(h => new DashboardCountDto(Today.ToDateTime(TimeOnly.MinValue).AddHours(h), h % 3)).ToList()
            : Enumerable.Range(0, Today.Day).Select(i => new DashboardCountDto(FirstOfMonth.AddDays(i).ToDateTime(TimeOnly.MinValue), i % 5)).ToList();
        var from = bucket == DashboardBuckets.Hour ? Today : FirstOfMonth;
        return new OrderDashboardDto(
            new DashboardRangeDto(from, Today, Today.AddYears(-1), Today, bucket),
            stats ?? new OrderStatsDto(1, 1, 1),
            points,
            top ?? []);
    }

    private static async Task<(DashboardViewModel Page, Window Window)> ShowAsync(TestServices app)
    {
        var page = Assert.IsType<DashboardViewModel>(app.Navigation.CreatePage(typeof(DashboardViewModel)));
        var window = app.Show(page);
        await page.ActivateAsync();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (page, window);
    }

    [AvaloniaFact]
    public async Task Admin_dashboard_loads_this_month_in_one_request_and_counts_orders_by_payment_status()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.OnGet(DashboardPath, Sample(new OrderStatsDto(Paid: 6, PartiallyPaid: 3, Unpaid: 1)));

        var (page, window) = await ShowAsync(app);

        var request = Assert.Single(app.Api.RequestsTo(HttpMethod.Get, "/api/orders"));
        Assert.Equal($"?from={FirstOfMonth:yyyy-MM-dd}&to={Today:yyyy-MM-dd}", request.RequestUri!.Query);
        Assert.Equal((10, 6, 3, 1), (page.TotalOrders, page.PaidOrders, page.PartiallyPaidOrders, page.UnpaidOrders));
        Assert.Equal(["10", "6", "3", "1"], window.GetVisualDescendants().OfType<StatCard>().Select(c => c.Value));
        Assert.Equal((6.0, 6.0), (Named<ProgressBar>(window, "PaidBar").Maximum, Named<ProgressBar>(window, "PaidBar").Value));
        Assert.Equal("60%", page.PaidPercent);
        Assert.Equal("30%", page.PartiallyPaidPercent);
        Assert.Equal("10%", page.UnpaidPercent);
        Assert.True(Named<StackPanel>(window, "PeriodCaption").IsEffectivelyVisible);
        Assert.Contains("(per day)", TestServices.Texts(window));
    }

    [AvaloniaFact]
    public async Task Admin_dashboard_draws_orders_per_day_of_the_period()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        var sample = Sample();
        app.Api.OnGet(DashboardPath, sample);

        var (page, window) = await ShowAsync(app);

        Assert.Equal(sample.Orders.Count, page.OrdersOverTime!.Count);
        Assert.All(page.OrdersOverTime, p => Assert.Equal(ChartUnit.Day, p.Unit));
        Assert.Equal(sample.Orders.Sum(d => d.Count), page.PeriodOrders);
        var chart = Named<LineChart>(window, "OrdersOverTimeChart");
        Assert.True(chart.IsEffectivelyVisible);
        Assert.Same(page.OrdersOverTime, chart.Points);
        Assert.Contains($"{page.PeriodOrders} orders created in this period", TestServices.Texts(window));
    }

    [AvaloniaFact]
    public async Task Admin_dashboard_draws_the_top_tests_line_graph_named_by_test()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        var tests = Enumerable.Range(0, 20)
            .Select(i => new TopTestDto(Guid.NewGuid(), $"T{i}", i == 0 ? "تحليل صورة دم كاملة / Complete blood count with differential" : $"Test number {i}", 40 - i))
            .ToList();
        app.Api.OnGet(DashboardPath, Sample(top: tests));

        var (page, window) = await ShowAsync(app);

        Assert.True(page.HasTopTests);
        Assert.Equal(20, page.TopTests!.Count);
        Assert.Equal((tests[0].Name, 40), (page.TopTests[0].Label, page.TopTests[0].Value));
        Assert.All(page.TopTests, p => Assert.Null(p.Time));
        var chart = Named<LineChart>(window, "TopTestsChart");
        Assert.True(chart.IsEffectivelyVisible);
        Assert.Same(page.TopTests, chart.Points);
        Assert.Contains("Top 20 most ordered tests", TestServices.Texts(window));
    }

    [AvaloniaFact]
    public async Task Choosing_a_day_reloads_that_day_per_hour_and_the_bounds_limit_the_pickers()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.On(HttpMethod.Get, DashboardPath, request => FakeApiHandler.Json(
            Sample(bucket: request.RequestUri!.Query.StartsWith($"?from={Today:yyyy-MM-dd}", StringComparison.Ordinal) && Today.Day != 1
                ? DashboardBuckets.Hour
                : DashboardBuckets.Day)));
        var (page, window) = await ShowAsync(app);
        Assert.Equal(Today.AddYears(-1).ToDateTime(TimeOnly.MinValue), page.Filter.MinDate);
        Assert.True(Named<ComboBox>(window, "MonthBox").IsEffectivelyVisible);

        page.Filter.SelectedPeriod = DashboardPeriodFilter.PeriodOptions.Single(o => o.Period == DashboardPeriod.Day);
        await WaitUntil(() => app.Api.RequestsTo(HttpMethod.Get, DashboardPath).Count() == 2 && !page.IsLoading);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal($"?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}", app.Api.Requests[^1].RequestUri!.Query);
        Assert.True(Named<CalendarDatePicker>(window, "DayPicker").IsEffectivelyVisible);
        Assert.False(Named<ComboBox>(window, "MonthBox").IsEffectivelyVisible);
        if (Today.Day != 1)
        {
            Assert.Equal(24, page.OrdersOverTime!.Count);
            Assert.All(page.OrdersOverTime, p => Assert.Equal(ChartUnit.Hour, p.Unit));
            Assert.True(page.IsSingleDay);
        }
    }

    [AvaloniaFact]
    public async Task A_failed_load_shows_the_error_state_and_refresh_loads_again()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.FailWithNetworkError();

        var (page, window) = await ShowAsync(app);

        Assert.True(page.HasLoadError);
        Assert.True(Named<ErrorState>(window, "DashboardError").IsEffectivelyVisible);

        app.Api.Respond = _ => FakeApiHandler.Json(Sample());
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.False(page.HasLoadError);
        Assert.Equal(3, page.TotalOrders);
    }

    [AvaloniaFact]
    public async Task Other_roles_get_no_order_figures_and_send_no_request()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);

        var page = Assert.IsType<DashboardViewModel>(app.Navigation.CreatePage(typeof(DashboardViewModel)));
        await page.ActivateAsync();

        Assert.False(page.ShowOrderStats);
        Assert.Empty(app.Api.RequestsTo(HttpMethod.Get, DashboardPath));
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(1, 4)]
    [InlineData(4, 4)]
    [InlineData(5, 8)]
    [InlineData(13, 16)]
    public void The_axis_top_is_a_multiple_of_the_gridlines_so_every_gridline_is_a_whole_number(int largest, int expected) =>
        Assert.Equal(expected, LineChart.AxisMaximum(largest));
}
