using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Maintenance;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Tests.Infrastructure;
using Mostawsef.Desktop.ViewModels.Super;
using static Mostawsef.Desktop.Tests.Orders.OrderPages;

namespace Mostawsef.Desktop.Tests.Shell;

[Collection(UiTestCollection.Name)]
public class SuperDashboardTests : UiTestBase
{
    private const string DashboardPath = "/api/system/dashboard";

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
    public async Task Shows_stat_cards_status_bars_and_five_daily_graphs_without_calling_admin_endpoints()
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

        Assert.Equal("?days=30", Assert.Single(app.Api.RequestsTo(HttpMethod.Get, DashboardPath)).RequestUri!.Query);
        Assert.DoesNotContain(app.Api.Requests, r => r.RequestUri!.AbsolutePath.StartsWith("/api/orders", StringComparison.Ordinal));

        Assert.Equal("1,234.50", Named<StatCard>(window, "RevenueCard").Value);
        Assert.Equal("EGP", Named<StatCard>(window, "RevenueCard").Caption);
        Assert.Equal("300.00", Named<StatCard>(window, "OutstandingCard").Value);
        Assert.Equal("10", Named<StatCard>(window, "OrdersCard").Value);
        Assert.Equal("3 active", Named<StatCard>(window, "UsersCard").Caption);
        Assert.Equal("4", Named<StatCard>(window, "ErrorsTodayCard").Value);

        Assert.Equal((5.0, 5.0), (Named<ProgressBar>(window, "PaidBar").Maximum, Named<ProgressBar>(window, "PaidBar").Value));
        Assert.Equal("50%", page.PaidPercent);
        Assert.Equal("10%", page.RefundedPercent);

        foreach (var name in new[] { "RevenueChart", "OrdersChart", "PatientsChart", "ActivityChart", "ErrorsChart" })
        {
            var chart = Named<LineChart>(window, name);
            Assert.True(chart.IsEffectivelyVisible, name);
            Assert.Equal(30, chart.Points!.Count);
        }

        // Revenue is drawn in whole currency units: 250.75 rounds to 251.
        Assert.Equal(251, page.RevenuePerDay![^1].Value);
        Assert.Equal(dashboard.Days.Sum(d => d.Activity), page.PeriodActivity);
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

    private static SuperDashboardDto Sample()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var days = Enumerable.Range(0, 30)
            .Select(i => new DashboardDayDto(today.AddDays(i - 29), i % 3, i % 2, i == 29 ? 25_075 : 1000 * i, i * 4, i % 4))
            .ToList();
        return new SuperDashboardDto(
            "EGP", Users: 4, ActiveUsers: 3, Patients: 25, LabTests: 40, Orders: 10,
            Revenue: 123_450, Outstanding: 30_000, ActivityToday: 116, ErrorsToday: 4,
            [
                new(OrderStatuses.Pending, 1), new(OrderStatuses.PartiallyPaid, 2), new(OrderStatuses.Paid, 5),
                new(OrderStatuses.Cancelled, 1), new(OrderStatuses.Refunded, 1),
            ],
            days);
    }
}
