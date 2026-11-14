using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Shell;
using static Mostawsef.Desktop.Tests.Orders.OrderPages;

namespace Mostawsef.Desktop.Tests.Shell;

[Collection(UiTestCollection.Name)]
public class DashboardStatsTests : UiTestBase
{
    [AvaloniaFact]
    public async Task Admin_dashboard_counts_orders_by_payment_status()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.OnGet("/api/orders/stats", new OrderStatsDto(Paid: 6, PartiallyPaid: 3, Unpaid: 1));

        var page = Assert.IsType<DashboardViewModel>(app.Navigation.CreatePage(typeof(DashboardViewModel)));
        var window = app.Show(page);
        await page.ActivateAsync();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal((10, 6, 3, 1), (page.TotalOrders, page.PaidOrders, page.PartiallyPaidOrders, page.UnpaidOrders));
        Assert.Equal(["10", "6", "3", "1"], window.GetVisualDescendants().OfType<StatCard>().Select(c => c.Value));
        Assert.Equal((6.0, 6.0), (Named<ProgressBar>(window, "PaidBar").Maximum, Named<ProgressBar>(window, "PaidBar").Value));
        Assert.Equal("60%", page.PaidPercent);
        Assert.Equal("30%", page.PartiallyPaidPercent);
        Assert.Equal("10%", page.UnpaidPercent);
    }

    [AvaloniaFact]
    public async Task Admin_dashboard_draws_the_orders_per_day_line_graph()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var days = Enumerable.Range(0, 30).Select(i => new OrderDailyCountDto(today.AddDays(i - 29), i % 5)).ToList();
        app.Api.OnGet("/api/orders/stats", new OrderStatsDto(1, 1, 1));
        app.Api.OnGet("/api/orders/daily", days);

        var page = Assert.IsType<DashboardViewModel>(app.Navigation.CreatePage(typeof(DashboardViewModel)));
        var window = app.Show(page);
        await page.ActivateAsync();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.Equal(30, page.Daily!.Count);
        Assert.Equal(days.Sum(d => d.Count), page.DailyTotal);
        var chart = Named<LineChart>(window, "DailyOrdersChart");
        Assert.True(chart.IsEffectivelyVisible);
        Assert.Same(page.Daily, chart.Points);
        Assert.Contains($"{page.DailyTotal} orders created in the last 30 days", TestServices.Texts(window));
    }

    [AvaloniaFact]
    public async Task Admin_dashboard_draws_the_top_tests_line_graph_named_by_test()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        var tests = Enumerable.Range(0, 20)
            .Select(i => new TopTestDto(Guid.NewGuid(), $"T{i}", i == 0 ? "تحليل صورة دم كاملة / Complete blood count with differential" : $"Test number {i}", 40 - i))
            .ToList();
        app.Api.OnGet("/api/orders/stats", new OrderStatsDto(1, 1, 1));
        app.Api.OnGet("/api/orders/top-tests", tests);

        var page = Assert.IsType<DashboardViewModel>(app.Navigation.CreatePage(typeof(DashboardViewModel)));
        var window = app.Show(page);
        await page.ActivateAsync();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.True(page.HasTopTests);
        Assert.Equal(20, page.TopTests!.Count);
        Assert.Equal((tests[0].Name, 40), (page.TopTests[0].Label, page.TopTests[0].Value));
        Assert.All(page.TopTests, p => Assert.Null(p.Date));
        var chart = Named<LineChart>(window, "TopTestsChart");
        Assert.True(chart.IsEffectivelyVisible);
        Assert.Same(page.TopTests, chart.Points);
        Assert.Contains("Top 20 most ordered tests", TestServices.Texts(window));
    }

    [AvaloniaFact]
    public async Task The_graph_is_hidden_when_the_daily_counts_cannot_be_loaded()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Admin);
        app.Api.OnGet("/api/orders/stats", new OrderStatsDto(1, 1, 1));

        var page = Assert.IsType<DashboardViewModel>(app.Navigation.CreatePage(typeof(DashboardViewModel)));
        var window = app.Show(page);
        await page.ActivateAsync();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(page.HasDaily);
        Assert.False(Named<LineChart>(window, "DailyOrdersChart").IsEffectivelyVisible);
        Assert.Equal(3, page.TotalOrders);
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
