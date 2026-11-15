using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Dashboards;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Diagnostics;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// Home page of every portal: the welcome line and, for Admin, the orders of a chosen period (<see cref="DashboardPageViewModel.Filter"/>):
/// how many are paid, partly paid or unpaid, orders per hour/day/month and the most ordered tests. One request per load;
/// showing the page again reloads, so new orders appear.
/// Shared by all roles until one needs its own dashboard; then give that role a separate view model + view in AppRoutes.
/// </summary>
public partial class DashboardViewModel(SessionService session, OrdersApi orders, ErrorReporter errors)
    : DashboardPageViewModel(errors), ILoadablePage
{
    public string UserName => session.CurrentUser?.Username ?? string.Empty;

    /// <summary>Only Admin sees (and may load) the order statistics.</summary>
    public bool ShowOrderStats => session.IsInRole(Roles.Admin);

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(Stats), nameof(HasOrders), nameof(TotalOrders), nameof(PaidOrders), nameof(PartiallyPaidOrders), nameof(UnpaidOrders),
        nameof(ChartMaximum), nameof(PaidPercent), nameof(PartiallyPaidPercent), nameof(UnpaidPercent),
        nameof(OrdersOverTime), nameof(HasOrdersOverTime), nameof(PeriodOrders), nameof(TopTests), nameof(HasTopTests))]
    public partial OrderDashboardDto? Dashboard { get; private set; }

    public OrderStatsDto? Stats => Dashboard?.Stats;

    public bool HasOrders => TotalOrders > 0;

    public int TotalOrders => Stats?.Total ?? 0;

    public int PaidOrders => Stats?.Paid ?? 0;

    public int PartiallyPaidOrders => Stats?.PartiallyPaid ?? 0;

    public int UnpaidOrders => Stats?.Unpaid ?? 0;

    /// <summary>The bars share one scale: the largest group fills the track.</summary>
    public double ChartMaximum => Math.Max(1, Math.Max(PaidOrders, Math.Max(PartiallyPaidOrders, UnpaidOrders)));

    public string PaidPercent => Percent(PaidOrders);

    public string PartiallyPaidPercent => Percent(PartiallyPaidOrders);

    public string UnpaidPercent => Percent(UnpaidOrders);

    // Derived once per load in OnDashboardChanged, so the getters stay O(1).

    /// <summary>Orders created per hour, day or month of the period (zero-filled), oldest first.</summary>
    public IReadOnlyList<ChartPoint>? OrdersOverTime { get; private set; }

    public bool HasOrdersOverTime => OrdersOverTime is { Count: > 0 };

    /// <summary>Orders created in the period.</summary>
    public int PeriodOrders { get; private set; }

    /// <summary>The most ordered tests of the period, most first: each point is a test name and how many times it was ordered.</summary>
    public IReadOnlyList<ChartPoint>? TopTests { get; private set; }

    public bool HasTopTests => TopTests is { Count: > 0 };

    /// <summary>Loads the chosen period every time the page is shown, so new orders appear.</summary>
    public Task ActivateAsync() => ShowOrderStats ? LoadAsync() : Task.CompletedTask;

    protected override async Task<DashboardRangeDto?> FetchAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var result = await orders.GetDashboardAsync(from, to, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.IsSuccess)
        {
            return null;
        }

        Dashboard = result.Value;
        return result.Value!.Range;
    }

    /// <summary>Runs before the generated change notifications, so the bindings read the new values.</summary>
    partial void OnDashboardChanged(OrderDashboardDto? value)
    {
        if (value is null)
        {
            (OrdersOverTime, TopTests, PeriodOrders) = (null, null, 0);
            return;
        }

        var unit = DashboardPeriodFilter.UnitOf(value.Range.Bucket);
        var points = new List<ChartPoint>(value.Orders.Count);
        var total = 0;
        foreach (var point in value.Orders)
        {
            points.Add(ChartPoint.At(point.Start, unit, point.Count));
            total += point.Count;
        }

        var top = new List<ChartPoint>(value.TopTests.Count);
        foreach (var test in value.TopTests)
        {
            top.Add(new ChartPoint(test.Name, test.Count));
        }

        (OrdersOverTime, PeriodOrders, TopTests) = (points, total, top);
    }

    private string Percent(int count) =>
        TotalOrders == 0 ? "0%" : string.Create(CultureInfo.InvariantCulture, $"{Math.Round(100.0 * count / TotalOrders):0}%");
}
