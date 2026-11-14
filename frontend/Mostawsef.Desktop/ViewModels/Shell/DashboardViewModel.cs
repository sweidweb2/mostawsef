using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Auth;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Auth;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// Home page of every portal: the welcome line and, for Admin, how many orders are paid, partly paid or unpaid.
/// Shared by all roles until one needs its own dashboard; then give that role a separate view model + view in AppRoutes.
/// </summary>
public partial class DashboardViewModel(SessionService session, OrdersApi orders) : ViewModelBase, ILoadablePage
{
    private CancellationTokenSource? _loadCancellation;

    public string UserName => session.CurrentUser?.Username ?? string.Empty;

    /// <summary>Only Admin sees (and may load) the order statistics.</summary>
    public bool ShowOrderStats => session.IsInRole(Roles.Admin);

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasOrders), nameof(TotalOrders), nameof(PaidOrders), nameof(PartiallyPaidOrders), nameof(UnpaidOrders),
        nameof(ChartMaximum), nameof(PaidPercent), nameof(PartiallyPaidPercent), nameof(UnpaidPercent))]
    public partial OrderStatsDto? Stats { get; private set; }

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

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

    /// <summary>Loads (or refreshes) the statistics every time the page is shown, so new orders appear.</summary>
    public async Task ActivateAsync()
    {
        if (!ShowOrderStats)
        {
            return;
        }

        _loadCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;

        IsLoading = Stats is null;
        try
        {
            var result = await orders.GetStatsAsync(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (result.IsSuccess)
            {
                Stats = result.Value;
            }
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

    public void Deactivate()
    {
        _loadCancellation?.Cancel();
        IsLoading = false;
    }

    private string Percent(int count) =>
        TotalOrders == 0 ? "0%" : string.Create(CultureInfo.InvariantCulture, $"{Math.Round(100.0 * count / TotalOrders):0}%");
}
