using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Orders;
using Mostawsef.Desktop.Services.Api;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Services.Ui;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.ViewModels.Admin;

/// <summary>
/// Add or edit a payment on an existing order. A new payment cannot exceed the balance; an edit cannot exceed
/// the balance plus that payment's current amount. Sends the order's Version so a concurrent payment change is refused.
/// </summary>
public partial class AddPaymentFormViewModel : FormViewModel
{
    private readonly OrdersApi _orders;
    private readonly Guid _orderId;
    private readonly int _orderVersion;
    private readonly long _maxMinor;
    private readonly Guid? _paymentId;

    /// <param name="existing">When set, the form edits that payment; otherwise it adds a new one.</param>
    public AddPaymentFormViewModel(
        OrdersApi orders,
        IDialogService dialogs,
        LocalizationService localization,
        Guid orderId,
        long balance,
        int orderVersion,
        OrderPaymentDto? existing = null)
        : base(dialogs, localization)
    {
        _orders = orders;
        _orderId = orderId;
        _orderVersion = orderVersion;
        _paymentId = existing?.Id;
        var room = existing is null ? balance : balance + existing.Amount;
        _maxMinor = Math.Min(room, OrderRules.MaxPaymentAmount);
        MaxAmount = Money.ToMajor(_maxMinor);

        if (existing is not null)
        {
            Amount = Money.ToMajor(existing.Amount);
            Method = existing.Method;
            Notes = existing.Notes ?? string.Empty;
        }

        MarkClean();
    }

    /// <summary>True when the form edits an existing payment instead of adding one.</summary>
    public bool IsEditing => _paymentId is not null;

    /// <summary>What is still due (or due plus this row when editing), in currency units: the most this payment may be.</summary>
    public decimal MaxAmount { get; }

    public IReadOnlyList<string> MethodOptions => PaymentMethods.All;

    /// <summary>In currency units; the request carries minor units.</summary>
    [ObservableProperty]
    public partial decimal? Amount { get; set; }

    [ObservableProperty]
    public partial string Method { get; set; } = PaymentMethods.Cash;

    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    protected override object BuildRequest() =>
        _paymentId is not null
            ? OrderPaymentMapping.ToUpdate(Amount, Method, Notes, _orderVersion)
            : OrderPaymentMapping.ToAdd(Amount, Method, Notes, _orderVersion);

    protected override void ValidateMore(FormErrors errors)
    {
        if (errors[nameof(Amount)] is not null)
        {
            return;
        }

        var amount = OrderPaymentMapping.ToMinorAmount(Amount) ?? 0;
        if (amount > _maxMinor)
        {
            errors.Set(nameof(Amount), ErrorCodes.ExceedsBalance);
        }
    }

    protected override async Task<IApiResult> SubmitAsync(object request, CancellationToken cancellationToken) =>
        _paymentId is { } paymentId
            ? await _orders.UpdatePaymentAsync(_orderId, paymentId, (UpdatePaymentRequest)request, cancellationToken)
            : await _orders.AddPaymentAsync(
                _orderId,
                (AddPaymentRequest)request with { ClientRequestId = WriteAttemptId },
                cancellationToken);
}
