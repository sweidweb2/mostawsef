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

/// <summary>An additional payment on an existing order; it can never be more than what the order still owes.</summary>
public partial class AddPaymentFormViewModel : FormViewModel
{
    private readonly OrdersApi _orders;
    private readonly Guid _orderId;
    private readonly long _balance;

    public AddPaymentFormViewModel(
        OrdersApi orders, IDialogService dialogs, LocalizationService localization, Guid orderId, long balance)
        : base(dialogs, localization)
    {
        _orders = orders;
        _orderId = orderId;
        _balance = balance;
        MaxAmount = Money.ToMajor(Math.Min(balance, OrderRules.MaxPaymentAmount));
        MarkClean();
    }

    /// <summary>What is still due, in currency units: the most this payment may be.</summary>
    public decimal MaxAmount { get; }

    public IReadOnlyList<string> MethodOptions => PaymentMethods.All;

    /// <summary>In currency units; the request carries minor units.</summary>
    [ObservableProperty]
    public partial decimal? Amount { get; set; }

    [ObservableProperty]
    public partial string Method { get; set; } = PaymentMethods.Cash;

    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    protected override object BuildRequest() => new AddPaymentRequest
    {
        Amount = Amount is { } amount ? Money.ToMinor(amount) : null,
        Method = Method,
        Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
    };

    protected override void ValidateMore(FormErrors errors)
    {
        if (errors[nameof(Amount)] is null && ((AddPaymentRequest)BuildRequest()).Amount > _balance)
        {
            errors.Set(nameof(Amount), ErrorCodes.ExceedsBalance);
        }
    }

    protected override async Task<IApiResult> SubmitAsync(object request, CancellationToken cancellationToken) =>
        await _orders.AddPaymentAsync(_orderId, (AddPaymentRequest)request, cancellationToken);
}
