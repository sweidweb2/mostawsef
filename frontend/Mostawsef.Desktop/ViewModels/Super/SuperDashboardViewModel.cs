using System;
using System.Collections.Generic;
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
/// orders by status, and per-day graphs of revenue, orders, new patients, activity and errors. Reloaded every time
/// the page is shown.
/// </summary>
public partial class SuperDashboardViewModel(SessionService session, SystemApi api) : ViewModelBase, ILoadablePage
{
    /// <summary>How many days the graphs cover.</summary>
    public const int Days = 30;

    private CancellationTokenSource? _loadCancellation;

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

    /// <summary>Net payments over the days shown.</summary>
    public string PeriodRevenueText => Amount(Dashboard?.Days.Sum(d => d.Revenue) ?? 0);

    // Orders by status

    public int PaidCount => StatusCount(OrderStatuses.Paid);

    public int PartiallyPaidCount => StatusCount(OrderStatuses.PartiallyPaid);

    public int PendingCount => StatusCount(OrderStatuses.Pending);

    public int CancelledCount => StatusCount(OrderStatuses.Cancelled);

    public int RefundedCount => StatusCount(OrderStatuses.Refunded);

    public bool HasOrders => (Dashboard?.Orders ?? 0) > 0;

    /// <summary>The status bars share one scale: the largest status fills the track.</summary>
    public double StatusMaximum => Math.Max(1, Dashboard?.OrderStatuses.Max(s => s.Count) ?? 0);

    public string PaidPercent => Percent(PaidCount);

    public string PartiallyPaidPercent => Percent(PartiallyPaidCount);

    public string PendingPercent => Percent(PendingCount);

    public string CancelledPercent => Percent(CancelledCount);

    public string RefundedPercent => Percent(RefundedCount);

    // Per-day graphs (oldest first, today last)

    /// <summary>Net payments per day in whole currency units (a day where refunds exceed payments shows 0).</summary>
    public IReadOnlyList<ChartPoint>? RevenuePerDay { get; private set; }

    public IReadOnlyList<ChartPoint>? OrdersPerDay { get; private set; }

    public IReadOnlyList<ChartPoint>? PatientsPerDay { get; private set; }

    public IReadOnlyList<ChartPoint>? ActivityPerDay { get; private set; }

    public IReadOnlyList<ChartPoint>? ErrorsPerDay { get; private set; }

    public int PeriodOrders => Dashboard?.Days.Sum(d => d.Orders) ?? 0;

    public int PeriodPatients => Dashboard?.Days.Sum(d => d.Patients) ?? 0;

    public int PeriodActivity => Dashboard?.Days.Sum(d => d.Activity) ?? 0;

    public int PeriodErrors => Dashboard?.Days.Sum(d => d.Errors) ?? 0;

    public Task ActivateAsync() => LoadAsync();

    /// <summary>Runs before the generated change notifications, so the graphs read the new series.</summary>
    partial void OnDashboardChanged(SuperDashboardDto? value)
    {
        RevenuePerDay = Series(value, d => (int)Math.Clamp(Math.Round(Money.ToMajor(d.Revenue)), 0, int.MaxValue));
        OrdersPerDay = Series(value, d => d.Orders);
        PatientsPerDay = Series(value, d => d.Patients);
        ActivityPerDay = Series(value, d => d.Activity);
        ErrorsPerDay = Series(value, d => d.Errors);
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

    private int StatusCount(string status) => Dashboard?.OrderStatuses.FirstOrDefault(s => s.Status == status)?.Count ?? 0;

    private static List<ChartPoint>? Series(SuperDashboardDto? dashboard, Func<DashboardDayDto, int> value) =>
        dashboard?.Days.Select(d => ChartPoint.Day(d.Date, value(d))).ToList();

    private string Percent(int count)
    {
        var total = Dashboard?.OrderStatuses.Sum(s => s.Count) ?? 0;
        return total == 0 ? "0%" : string.Create(CultureInfo.InvariantCulture, $"{Math.Round(100.0 * count / total):0}%");
    }

    private static string Amount(long minorUnits) => Money.ToMajor(minorUnits).ToString("N2", CultureInfo.InvariantCulture);
}
