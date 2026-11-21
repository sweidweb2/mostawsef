using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Converters;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Navigation;
using Mostawsef.Desktop.Services.Printing;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>Admin portal sub-page: one order's main details and tests (read-only). Set <see cref="OrderId"/> before it opens; optionally <see cref="Seed"/> to skip the first GET.</summary>
public partial class OrderDetailsViewModel(
    OrdersApi orders,
    NavigationService navigation,
    InvoiceService invoices,
    IDialogService dialogs,
    LocalizationService localization,
    IToastService toasts) : ViewModelBase, ILoadablePage, IReusablePage, IBreadcrumbPage
{
    private readonly LatestLoad _load = new();

    public Guid OrderId { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOrder), nameof(HasAddon), nameof(HasPayments), nameof(CanAddPayment), nameof(NeedsRefund), nameof(RefundText), nameof(BreadcrumbArg))]
    public partial OrderDetailsDto? Order { get; private set; }

    public bool HasOrder => Order is not null;

    /// <summary>Money still owed on the order can be paid with an additional payment.</summary>
    public bool CanAddPayment => Order is { Balance: > 0 };

    /// <summary>The order total fell below what was paid (e.g. it was altered): the difference must be given back.</summary>
    public bool NeedsRefund => Order is { Balance: < 0 };

    public string RefundText => Order is { Balance: < 0 } order
        ? localization.Format("OrderDetails.RefundNeeded", ValueConverters.FormatMoney(-order.Balance))
        : string.Empty;

    public bool HasPayments => Order is { Payments.Count: > 0 };

    public string BreadcrumbKey => "OrderDetails.Title";

    public string? BreadcrumbArg => Order?.Number;

    /// <summary>
    /// True when our price is part of the lab-inclusive total (hidden for legacy rows where Addon only mirrors Total).
    /// </summary>
    public bool HasAddon => Order is { } order && OrderPricing.ShowsAddon(order);

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError), nameof(LoadErrorTitleKey), nameof(LoadErrorDescriptionKey))]
    public partial ApiError? LoadError { get; private set; }

    public bool HasLoadError => LoadError is not null;

    public string? LoadErrorTitleKey => LoadError switch
    {
        null => null,
        ApiError.Unreachable => "Error.UnreachableTitle",
        ApiError.NotFound => "OrderDetails.NotFound",
        _ => "Error.Title",
    };

    public string? LoadErrorDescriptionKey => LoadError switch
    {
        null => null,
        ApiError.Unreachable => "Error.UnreachableDescription",
        ApiError.NotFound => null,
        _ => "Error.Description",
    };

    /// <summary>Clears prior order state so Orders can reuse one instance (and its PageHost view) per portal visit.</summary>
    public void BeginReuse()
    {
        _load.Cancel();
        OrderId = default;
        Order = null;
        LoadError = null;
        IsLoading = false;
    }

    /// <summary>Shows <paramref name="order"/> immediately (e.g. after create) so Activate skips the GET.</summary>
    public void Seed(OrderDetailsDto order)
    {
        _load.Cancel();
        OrderId = order.Id;
        Order = order;
        LoadError = null;
        IsLoading = false;
    }

    public Task ActivateAsync() => Order is null ? LoadAsync() : Task.CompletedTask;

    public void Deactivate()
    {
        _load.Cancel();
        IsLoading = false;
        Order = null;
    }

    /// <summary>True while an invoice is being rendered; its buttons show a spinner and wait.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseInvoice))]
    public partial bool IsInvoiceBusy { get; private set; }

    public bool CanUseInvoice => !IsInvoiceBusy;

    [RelayCommand]
    private Task PrintInvoiceAsync() => Order is { } order ? WithInvoiceBusyAsync(() => invoices.PrintAsync(order)) : Task.CompletedTask;

    [RelayCommand]
    private async Task ExportInvoiceAsync()
    {
        if (Order is not { } order || await invoices.AskExportPathAsync(order.Number) is not { } path)
        {
            return;
        }

        await WithInvoiceBusyAsync(() => invoices.ExportAsync(order, path));
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
    private async Task AddPaymentAsync()
    {
        if (Order is not { Balance: > 0 } order)
        {
            return;
        }

        var form = new AddPaymentFormViewModel(orders, dialogs, localization, order.Id, order.Balance);
        if (await dialogs.ShowFormAsync(form, localization.Get("OrderDetails.AddPaymentTitle")) && form.SavedValue is OrderDetailsDto updated)
        {
            ApplyOrder(updated, "OrderDetails.PaymentAdded");
        }
    }

    [RelayCommand]
    private async Task EditPaymentAsync(OrderPaymentDto? payment)
    {
        if (Order is not { } order || payment is null)
        {
            return;
        }

        var form = new AddPaymentFormViewModel(orders, dialogs, localization, order.Id, order.Balance, payment);
        if (await dialogs.ShowFormAsync(form, localization.Get("OrderDetails.EditPaymentTitle")) && form.SavedValue is OrderDetailsDto updated)
        {
            ApplyOrder(updated, "OrderDetails.PaymentUpdated");
        }
    }

    [RelayCommand]
    private async Task DeletePaymentAsync(OrderPaymentDto? payment)
    {
        if (Order is not { } order || payment is null
            || !await dialogs.ConfirmAsync(
                localization.Get("OrderDetails.DeletePaymentConfirm"),
                confirmText: localization.Get("Common.Delete"),
                isDanger: true))
        {
            return;
        }

        var result = await orders.DeletePaymentAsync(order.Id, payment.Id);
        if (!result.IsSuccess)
        {
            var code = result.Problem?.Code;
            var codeKey = code is null ? null : "ErrorCode." + code;
            toasts.Error(localization.Get(
                result.Error == ApiError.Unreachable ? "Login.ServerUnreachable"
                : codeKey is not null && localization.Get(codeKey) != codeKey ? codeKey
                : "Error.RequestFailed"));
            return;
        }

        ApplyOrder(result.Value!, "OrderDetails.PaymentDeleted");
    }

    private void ApplyOrder(OrderDetailsDto updated, string successKey)
    {
        Order = updated;
        LoadError = null;
        WeakReferenceMessenger.Default.Send(new OrderChangedMessage(updated));
        toasts.Success(localization.Get(successKey));
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private Task<bool> BackAsync() => navigation.BackAsync();

    private async Task LoadAsync()
    {
        using var cancellation = _load.Begin();

        IsLoading = true;
        try
        {
            var result = await orders.GetOrderAsync(OrderId, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            Order = result.Value;
            LoadError = result.IsSuccess ? null : result.Error;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The user left the page; the next activation loads again.
        }
        finally
        {
            if (_load.Finish(cancellation))
            {
                IsLoading = false;
            }
        }
    }
}
