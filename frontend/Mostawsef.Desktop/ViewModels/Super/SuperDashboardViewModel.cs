using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Dashboards;
using Mostawsef.Contracts.Maintenance;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Super;

/// <summary>
/// Super portal home for a chosen period (<see cref="DashboardPageViewModel.Filter"/>): revenue, outstanding balance,
/// orders, new patients, activity and errors of the period, current user and lab test totals, orders by status and
/// per hour/day/month graphs. Coming back to the page reloads only when the figures are older than
/// <see cref="StaleAfter"/> (Refresh and a new period always reload), so switching menu pages does not run the
/// dashboard queries again each time.
/// </summary>
public partial class SuperDashboardViewModel(SessionService session, SystemApi api, ErrorReporter errors)
    : DashboardPageViewModel(errors), ILoadablePage
{
    /// <summary>How old the shown figures may get before showing the page loads them again.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(1);

    /// <summary>Stopwatch timestamp of the last successful load; 0 = never.</summary>
    private long _loadedAt;

    public string UserName => session.CurrentUser?.Username ?? string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasData), nameof(CurrencyKey), nameof(RevenueText), nameof(OutstandingText),
        nameof(PaidCount), nameof(PartiallyPaidCount), nameof(PendingCount), nameof(CancelledCount), nameof(RefundedCount),
        nameof(StatusMaximum), nameof(HasOrders),
        nameof(PaidPercent), nameof(PartiallyPaidPercent), nameof(PendingPercent), nameof(CancelledPercent), nameof(RefundedPercent),
        nameof(RevenueOverTime), nameof(OrdersOverTime), nameof(PatientsOverTime), nameof(ActivityOverTime), nameof(ErrorsOverTime))]
    public partial SuperDashboardDto? Dashboard { get; private set; }

    public bool HasData => Dashboard is not null;

    /// <summary>"Currency.EGP": the stat cards' money caption, translated in the view.</summary>
    public string? CurrencyKey => Dashboard is null ? null : "Currency." + Dashboard.Currency;

    /// <summary>Payments minus refunds made in the period.</summary>
    public string RevenueText => Amount(Dashboard?.Revenue ?? 0);

    /// <summary>What the period's pending and partly paid orders still owe.</summary>
    public string OutstandingText => Amount(Dashboard?.Outstanding ?? 0);

    // Everything below is derived once per load in OnDashboardChanged, so the getters stay O(1).

    // Orders by status

    public int PaidCount { get; private set; }

    public int PartiallyPaidCount { get; private set; }

    public int PendingCount { get; private set; }

    public int CancelledCount { get; private set; }

    public int RefundedCount { get; private set; }

    public bool HasOrders => (Dashboard?.Orders ?? 0) > 0;

    /// <summary>The status bars share one scale: the largest status fills the track.</summary>
    public double StatusMaximum { get; private set; } = 1;

    public string PaidPercent { get; private set; } = "0%";

    public string PartiallyPaidPercent { get; private set; } = "0%";

    public string PendingPercent { get; private set; } = "0%";

    public string CancelledPercent { get; private set; } = "0%";

    public string RefundedPercent { get; private set; } = "0%";

    // Graphs (oldest first): one point per hour, day or month of the period

    /// <summary>Net payments per point in whole currency units (a point where refunds exceed payments shows 0).</summary>
    public IReadOnlyList<ChartPoint>? RevenueOverTime { get; private set; }

    public IReadOnlyList<ChartPoint>? OrdersOverTime { get; private set; }

    public IReadOnlyList<ChartPoint>? PatientsOverTime { get; private set; }

    public IReadOnlyList<ChartPoint>? ActivityOverTime { get; private set; }

    public IReadOnlyList<ChartPoint>? ErrorsOverTime { get; private set; }

    public Task ActivateAsync() =>
        Dashboard is null || _loadedAt == 0 || Stopwatch.GetElapsedTime(_loadedAt) >= StaleAfter ? LoadAsync() : Task.CompletedTask;

    protected override async Task<DashboardRangeDto?> FetchAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var result = await api.GetDashboardAsync(from, to, cancellationToken);
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
    partial void OnDashboardChanged(SuperDashboardDto? value)
    {
        var count = value?.Points.Count ?? 0;
        var unit = DashboardPeriodFilter.UnitOf(value?.Range.Bucket);
        List<ChartPoint> revenue = new(count), orders = new(count), patients = new(count), activity = new(count), errors = new(count);
        foreach (var point in value?.Points ?? [])
        {
            revenue.Add(ChartPoint.At(point.Start, unit, (int)Math.Clamp(Math.Round(Money.ToMajor(point.Revenue)), 0, int.MaxValue)));
            orders.Add(ChartPoint.At(point.Start, unit, point.Orders));
            patients.Add(ChartPoint.At(point.Start, unit, point.Patients));
            activity.Add(ChartPoint.At(point.Start, unit, point.Activity));
            errors.Add(ChartPoint.At(point.Start, unit, point.Errors));
        }

        (RevenueOverTime, OrdersOverTime, PatientsOverTime, ActivityOverTime, ErrorsOverTime) =
            value is null ? (null, null, null, null, null) : (revenue, orders, patients, activity, errors);

        int paid = 0, partlyPaid = 0, pending = 0, cancelled = 0, refunded = 0, total = 0, largest = 0;
        foreach (var status in value?.OrderStatuses ?? [])
        {
            total += status.Count;
            largest = Math.Max(largest, status.Count);
            switch (status.Status)
            {
                case OrderStatuses.Paid: paid = status.Count; break;
                case OrderStatuses.PartiallyPaid: partlyPaid = status.Count; break;
                case OrderStatuses.Pending: pending = status.Count; break;
                case OrderStatuses.Cancelled: cancelled = status.Count; break;
                case OrderStatuses.Refunded: refunded = status.Count; break;
            }
        }

        (PaidCount, PartiallyPaidCount, PendingCount, CancelledCount, RefundedCount) = (paid, partlyPaid, pending, cancelled, refunded);
        StatusMaximum = Math.Max(1, largest);
        PaidPercent = Percent(paid, total);
        PartiallyPaidPercent = Percent(partlyPaid, total);
        PendingPercent = Percent(pending, total);
        CancelledPercent = Percent(cancelled, total);
        RefundedPercent = Percent(refunded, total);
    }

    private static string Percent(int count, int total) =>
        total == 0 ? "0%" : string.Create(CultureInfo.InvariantCulture, $"{Math.Round(100.0 * count / total):0}%");

    private static string Amount(long minorUnits) => Money.ToMajor(minorUnits).ToString("N2", CultureInfo.InvariantCulture);
}
