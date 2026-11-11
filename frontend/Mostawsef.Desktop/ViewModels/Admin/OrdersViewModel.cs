using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Diagnostics;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>Admin portal: orders, newest first, with search (patient name, phone or number) and paging; opens New order and Order details.</summary>
public partial class OrdersViewModel(OrdersApi orders, NavigationService navigation, IToastService toasts, LocalizationService localization, ErrorReporter errors)
    : NumberedListViewModel<OrderListItemDto>(toasts, localization, errors)
{
    /// <summary>One New order page for this portal visit; reset on each open so PageHost can reuse its view.</summary>
    private NewOrderViewModel? _newOrder;

    /// <summary>One Order details page for this portal visit; reset on each open so PageHost can reuse its view.</summary>
    private OrderDetailsViewModel? _orderDetails;

    protected override Task<ApiResult<PagedResult<OrderListItemDto>>> FetchPageAsync(
        int page, int pageSize, string? search, bool includeTotal, CancellationToken cancellationToken) =>
        orders.GetOrdersAsync(page, pageSize, search, includeTotal, cancellationToken);

    /// <summary>True while the New order button is waiting for the create page to open.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NewOrderCommand))]
    public partial bool IsOpeningNewOrder { get; private set; }

    private bool CanNewOrder() => !IsOpeningNewOrder;

    [RelayCommand(CanExecute = nameof(CanNewOrder))]
    private async Task NewOrderAsync()
    {
        IsOpeningNewOrder = true;
        try
        {
            // Let the button spinner paint before the (first) view build runs on the UI thread.
            await Task.Yield();

            _newOrder ??= navigation.CreatePage(typeof(NewOrderViewModel)) as NewOrderViewModel;
            _orderDetails ??= navigation.CreatePage(typeof(OrderDetailsViewModel)) as OrderDetailsViewModel;
            if (_newOrder is null)
            {
                return;
            }

            await navigation.OpenAsync(_newOrder, page =>
            {
                page.OnOrderCreated = InsertCreated;
                page.OrderDetailsPage = _orderDetails;
                page.BeginReuse();
            });
        }
        finally
        {
            IsOpeningNewOrder = false;
        }
    }

    [RelayCommand]
    private Task ViewAsync(OrderListItemDto? item)
    {
        if (item is null)
        {
            return Task.CompletedTask;
        }

        _orderDetails ??= navigation.CreatePage(typeof(OrderDetailsViewModel)) as OrderDetailsViewModel;
        return _orderDetails is null
            ? Task.CompletedTask
            : navigation.OpenAsync(_orderDetails, page =>
            {
                page.BeginReuse();
                page.OrderId = item.Id;
            });
    }

    /// <summary>Adds the new order on page 1 without wiping the table; falls back to a full reload when needed.</summary>
    private void InsertCreated(OrderDetailsDto order) =>
        PrependNewest(new OrderListItemDto(
            order.Id,
            order.Number,
            order.CreatedAt,
            order.PatientName,
            order.PatientPhone,
            order.Items.Count,
            order.Total,
            order.Paid,
            order.Status));
}
