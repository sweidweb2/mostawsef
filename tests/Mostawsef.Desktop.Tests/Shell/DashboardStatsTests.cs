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
    public async Task Super_dashboard_does_not_load_order_statistics()
    {
        using var app = new TestServices();
        app.SignInAs(Roles.Super);

        var page = Assert.IsType<DashboardViewModel>(app.Navigation.CreatePage(typeof(DashboardViewModel)));
        await page.ActivateAsync();

        Assert.False(page.ShowOrderStats);
        Assert.DoesNotContain(app.Api.Requests, r => r.RequestUri!.AbsolutePath.StartsWith("/api/orders", StringComparison.Ordinal));
    }
}
