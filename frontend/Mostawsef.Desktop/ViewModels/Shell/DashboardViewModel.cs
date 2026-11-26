using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Dashboards;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Diagnostics;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// Home page of every portal: the welcome line and, for Admin, the money and orders of a chosen period
/// (<see cref="DashboardPageViewModel.Filter"/>): revenue and outstanding balance, how many orders are paid, partly
/// paid or unpaid, orders and revenue per hour/day/month and the most ordered tests. Coming back to the page reloads
/// only when the figures are older than <see cref="StaleAfter"/> (Refresh and a new period always reload).
/// Shared by all roles until one needs its own dashboard; then give that role a separate view model + view in AppRoutes.
/// </summary>
public partial class DashboardViewModel(SessionService session, OrdersApi orders, ErrorReporter errors)
    : DashboardPageViewModel(errors), ILoadablePage
{
    /// <summary>How old the shown figures may get before showing the page loads them again.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(1);

    /// <summary>Stopwatch timestamp of the last successful load; 0 = never.</summary>
    private long _loadedAt;

    public string UserName => session.CurrentUser?.Username ?? string.Empty;

    /// <summary>Only Admin sees (and may load) the order statistics.</summary>
    public bool ShowOrderStats => session.IsInRole(Roles.Admin);

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(Stats), nameof(HasOrders), nameof(TotalOrders), nameof(PaidOrders), nameof(PartiallyPaidOrders), nameof(UnpaidOrders),
        nameof(ChartMaximum), nameof(PaidPercent), nameof(PartiallyPaidPercent), nameof(UnpaidPercent),
        nameof(CurrencyKey), nameof(RevenueText), nameof(OutstandingText),
        nameof(OrdersOverTime), nameof(HasOrdersOverTime), nameof(PeriodOrders),
        nameof(RevenueOverTime), nameof(TopTests), nameof(HasTopTests))]
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

    /// <summary>"Currency.EGP": the money cards' caption, translated in the view.</summary>
    public string? CurrencyKey => Dashboard is null ? null : "Currency." + Dashboard.Currency;

    /// <summary>Payments minus refunds made in the period.</summary>
    public string RevenueText => Amount(Dashboard?.Revenue ?? 0);

    /// <summary>What the period's pending and partly paid orders still owe.</summary>
    public string OutstandingText => Amount(Dashboard?.Outstanding ?? 0);

    // Derived once per load in OnDashboardChanged, so the getters stay O(1).

    /// <summary>Orders created per hour, day or month of the period (zero-filled), oldest first.</summary>
    public IReadOnlyList<ChartPoint>? OrdersOverTime { get; private set; }

    public bool HasOrdersOverTime => OrdersOverTime is { Count: > 0 };

    /// <summary>Orders created in the period.</summary>
    public int PeriodOrders { get; private set; }

    /// <summary>Net payments per point in whole currency units (a point where refunds exceed payments shows 0).</summary>
    public IReadOnlyList<ChartPoint>? RevenueOverTime { get; private set; }

    /// <summary>The most ordered tests of the period, most first: each point is a test name and how many times it was ordered.</summary>
    public IReadOnlyList<ChartPoint>? TopTests { get; private set; }

    public bool HasTopTests => TopTests is { Count: > 0 };

    public Task ActivateAsync() =>
        !ShowOrderStats || (Dashboard is not null && _loadedAt != 0 && Stopwatch.GetElapsedTime(_loadedAt) < StaleAfter)
            ? Task.CompletedTask
            : LoadAsync();

    protected override async Task<DashboardRangeDto?> FetchAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var result = await orders.GetDashboardAsync(from, to, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.IsSuccess)
        {
            return null;
        }

        Dashboard = result.Value;
        _loadedAt = Stopwatch.GetTimestamp();
        return result.Value!.Range;
    }

    /// <summary>Runs before the generated change notifications, so the bindings read the new values.</summary>
    partial void OnDashboardChanged(OrderDashboardDto? value)
    {
        if (value is null)
        {
            (OrdersOverTime, RevenueOverTime, TopTests, PeriodOrders) = (null, null, null, 0);
            return;
        }

        var unit = DashboardPeriodFilter.UnitOf(value.Range.Bucket);
        var ordersOverTime = new List<ChartPoint>(value.Orders.Count);
        var total = 0;
        foreach (var point in value.Orders)
        {
            ordersOverTime.Add(ChartPoint.At(point.Start, unit, point.Count));
            total += point.Count;
        }

        var revenue = new List<ChartPoint>(value.RevenuePoints.Count);
        foreach (var point in value.RevenuePoints)
        {
            revenue.Add(ChartPoint.At(point.Start, unit, (int)Math.Clamp(Math.Round(Money.ToMajor(point.Amount)), 0, int.MaxValue)));
        }

        var top = new List<ChartPoint>(value.TopTests.Count);
        foreach (var test in value.TopTests)
        {
            top.Add(new ChartPoint(test.Name, test.Count));
        }

        (OrdersOverTime, RevenueOverTime, PeriodOrders, TopTests) = (ordersOverTime, revenue, total, top);
    }

    private string Percent(int count) =>
        TotalOrders == 0 ? "0%" : string.Create(CultureInfo.InvariantCulture, $"{Math.Round(100.0 * count / TotalOrders):0}%");

    private static string Amount(long minorUnits) => Money.ToMajor(minorUnits).ToString("N2", CultureInfo.InvariantCulture);
}
