using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
using Mostawsef.Desktop.Services.Printing;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>Admin portal: orders, newest first, with search (patient name, phone or number) and paging; opens New order and Order details.</summary>
public partial class OrdersViewModel(
    OrdersApi orders,
    NavigationService navigation,
    InvoiceService invoices,
    IToastService toasts,
    LocalizationService localization,
    ErrorReporter errors)
    : NumberedListViewModel<OrderListItemDto>(toasts, localization, errors)
{
    /// <summary>One New order page for this portal visit; reset on each open so PageHost can reuse its view.</summary>
    private NewOrderViewModel? _newOrder;

    /// <summary>One Order details page for this portal visit; reset on each open so PageHost can reuse its view.</summary>
    private OrderDetailsViewModel? _orderDetails;

    protected override Task<ApiResult<PagedResult<OrderListItemDto>>> FetchPageAsync(
        int page, int pageSize, string? search, bool includeTotal, CancellationToken cancellationToken) =>
        orders.GetOrdersAsync(page, pageSize, search, includeTotal, SelectedStatus?.Value, patientId: null, cancellationToken);

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

            var details = navigation.Reuse(ref _orderDetails);
            if (navigation.Reuse(ref _newOrder) is not { } newOrder)
            {
                return;
            }

            await navigation.OpenAsync(newOrder, page =>
            {
                page.OnOrderCreated = InsertCreated;
                page.OrderDetailsPage = details;
                page.BeginReuse();
            });
        }
        finally
        {
            IsOpeningNewOrder = false;
        }
    }

    /// <summary>The payment-status filter choices: everything first, then each status.</summary>
    public static IReadOnlyList<OrderStatusOption> StatusOptions { get; } =
    [
        new(null, "Orders.Filter.AllStatuses"),
        .. OrderStatuses.All.Select(status => new OrderStatusOption(status, "OrderStatus." + status)),
    ];

    /// <summary>The chosen status; changing it shows page 1 of the matching orders.</summary>
    [ObservableProperty]
    public partial OrderStatusOption? SelectedStatus { get; set; } = StatusOptions[0];

    protected override bool HasActiveFilter => base.HasActiveFilter || SelectedStatus?.Value is not null;

    partial void OnSelectedStatusChanged(OrderStatusOption? oldValue, OrderStatusOption? newValue)
    {
        if (oldValue is not null && newValue != oldValue)
        {
            _ = ApplyFilterAsync();
        }
    }

    /// <summary>True while an invoice is being fetched and rendered; the table shows its spinner meanwhile.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLoading))]
    public partial bool IsInvoiceBusy { get; private set; }

    /// <summary>The table's loading overlay: the list is loading or an invoice is being prepared.</summary>
    public bool ShowLoading => IsLoading || IsInvoiceBusy;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsLoading))
        {
            OnPropertyChanged(nameof(ShowLoading));
        }
    }

    /// <summary>Opens the print window with the order's invoice.</summary>
    [RelayCommand]
    private Task PrintInvoiceAsync(OrderListItemDto? item) =>
        item is null ? Task.CompletedTask : WithInvoiceBusyAsync(() => invoices.PrintAsync(item.Id));

    /// <summary>Asks where to save the order's invoice, then saves it there as a PDF.</summary>
    [RelayCommand]
    private async Task ExportInvoiceAsync(OrderListItemDto? item)
    {
        if (item is null || await invoices.AskExportPathAsync(item.Number) is not { } path)
        {
            return;
        }

        await WithInvoiceBusyAsync(() => invoices.ExportAsync(item.Id, path));
    }

    private async Task WithInvoiceBusyAsync(Func<Task> action)
    {
        IsInvoiceBusy = true;
        try
        {
            await action();
        }
        finally
        {
            IsInvoiceBusy = false;
        }
    }

    [RelayCommand]
    private Task ViewAsync(OrderListItemDto? item)
    {
        if (item is null)
        {
            return Task.CompletedTask;
        }

        return navigation.Reuse(ref _orderDetails) is { } details
            ? navigation.OpenAsync(details, page =>
            {
                page.BeginReuse();
                page.OrderId = item.Id;
            })
            : Task.CompletedTask;
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
