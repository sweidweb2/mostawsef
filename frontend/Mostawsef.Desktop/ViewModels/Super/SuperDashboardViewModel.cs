using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Maintenance;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Super;

/// <summary>
/// Super portal home: whole-system stat cards (money, orders, patients, users, tests, today's activity and errors),
/// orders by status, and per-day graphs of revenue, orders, new patients, activity and errors. Coming back to the page
/// reloads only when the figures are older than <see cref="StaleAfter"/> (Refresh always reloads), so switching menu
/// pages does not run the dashboard queries again each time.
/// </summary>
public partial class SuperDashboardViewModel(SessionService session, SystemApi api) : ViewModelBase, ILoadablePage
{
    /// <summary>How many days the graphs cover.</summary>
    public const int Days = 30;

    /// <summary>How old the shown figures may get before showing the page loads them again.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(1);

    private CancellationTokenSource? _loadCancellation;

    /// <summary>Stopwatch timestamp of the last successful load; 0 = never.</summary>
    private long _loadedAt;

    public string UserName => session.CurrentUser?.Username ?? string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasData), nameof(CurrencyKey), nameof(RevenueText), nameof(OutstandingText), nameof(PeriodRevenueText),
        nameof(PaidCount), nameof(PartiallyPaidCount), nameof(PendingCount), nameof(CancelledCount), nameof(RefundedCount),
        nameof(StatusMaximum), nameof(HasOrders),
        nameof(PaidPercent), nameof(PartiallyPaidPercent), nameof(PendingPercent), nameof(CancelledPercent), nameof(RefundedPercent),
        nameof(RevenuePerDay), nameof(OrdersPerDay), nameof(PatientsPerDay), nameof(ActivityPerDay), nameof(ErrorsPerDay),
        nameof(PeriodOrders), nameof(PeriodPatients), nameof(PeriodActivity), nameof(PeriodErrors))]
    public partial SuperDashboardDto? Dashboard { get; private set; }

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial bool HasLoadError { get; private set; }

    public bool HasData => Dashboard is not null;

    /// <summary>"Currency.EGP": the stat cards' money caption, translated in the view.</summary>
    public string? CurrencyKey => Dashboard is null ? null : "Currency." + Dashboard.Currency;

    public string RevenueText => Amount(Dashboard?.Revenue ?? 0);

    public string OutstandingText => Amount(Dashboard?.Outstanding ?? 0);

    // Everything below is derived once per load in OnDashboardChanged, so the getters stay O(1).

    /// <summary>Net payments over the days shown.</summary>
    public string PeriodRevenueText { get; private set; } = Amount(0);

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

    // Per-day graphs (oldest first, today last)

    /// <summary>Net payments per day in whole currency units (a day where refunds exceed payments shows 0).</summary>
    public IReadOnlyList<ChartPoint>? RevenuePerDay { get; private set; }

    public IReadOnlyList<ChartPoint>? OrdersPerDay { get; private set; }

    public IReadOnlyList<ChartPoint>? PatientsPerDay { get; private set; }

    public IReadOnlyList<ChartPoint>? ActivityPerDay { get; private set; }

    public IReadOnlyList<ChartPoint>? ErrorsPerDay { get; private set; }

    public int PeriodOrders { get; private set; }

    public int PeriodPatients { get; private set; }

    public int PeriodActivity { get; private set; }

    public int PeriodErrors { get; private set; }

    public Task ActivateAsync() =>
        Dashboard is null || _loadedAt == 0 || Stopwatch.GetElapsedTime(_loadedAt) >= StaleAfter ? LoadAsync() : Task.CompletedTask;
    /// <summary>Runs before the generated change notifications, so the bindings read the new values.</summary>
    partial void OnDashboardChanged(SuperDashboardDto? value)
    {
        RevenuePerDay = Series(value, d => (int)Math.Clamp(Math.Round(Money.ToMajor(d.Revenue)), 0, int.MaxValue));
        OrdersPerDay = Series(value, d => d.Orders);
        PatientsPerDay = Series(value, d => d.Patients);
        ActivityPerDay = Series(value, d => d.Activity);
        ErrorsPerDay = Series(value, d => d.Errors);

        long periodRevenue = 0;
        int orders = 0, patients = 0, activity = 0, errors = 0;
        foreach (var day in value?.Days ?? [])
        {
            periodRevenue += day.Revenue;
            orders += day.Orders;
            patients += day.Patients;
            activity += day.Activity;
            errors += day.Errors;
        }

        PeriodRevenueText = Amount(periodRevenue);
        (PeriodOrders, PeriodPatients, PeriodActivity, PeriodErrors) = (orders, patients, activity, errors);

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

    public void Deactivate()
    {
        _loadCancellation?.Cancel();
        IsLoading = false;
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        _loadCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;

        IsLoading = true;
        try
        {
            var result = await api.GetDashboardAsync(Days, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (result.IsSuccess)
            {
                Dashboard = result.Value;
                _loadedAt = Stopwatch.GetTimestamp();
            }

            HasLoadError = !result.IsSuccess && Dashboard is null;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The user left the page; the next activation loads again.
        }
        finally
        {
            if (_loadCancellation == cancellation)
            {
                _loadCancellation = null;
                IsLoading = false;
            }
        }
    }

    private static List<ChartPoint>? Series(SuperDashboardDto? dashboard, Func<DashboardDayDto, int> value) =>
        dashboard?.Days.Select(d => ChartPoint.Day(d.Date, value(d))).ToList();

    private static string Percent(int count, int total) =>
        total == 0 ? "0%" : string.Create(CultureInfo.InvariantCulture, $"{Math.Round(100.0 * count / total):0}%");

    private static string Amount(long minorUnits) => Money.ToMajor(minorUnits).ToString("N2", CultureInfo.InvariantCulture);
}
