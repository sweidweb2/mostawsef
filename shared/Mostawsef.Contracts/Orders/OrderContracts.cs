using System.ComponentModel.DataAnnotations;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Dashboards;
using Mostawsef.Contracts.LabTests;
using Mostawsef.Contracts.Patients;

namespace Mostawsef.Contracts.Orders;

/// <summary>Limits shared by the API and the desktop. Amounts are minor units of the clinic currency.</summary>
public static class OrderRules
{
    public const int PaymentNotesMaxLength = 500;

    /// <summary>Hard cap on tests per order (API + desktop).</summary>
    public const int MaxItems = 100;

    /// <summary>
    /// 10,000,000.00 in the clinic currency. One payment cannot exceed this; an order whose total is
    /// larger (labs plus addon) is settled with several payments.
    /// </summary>
    public const long MaxPaymentAmount = 1_000_000_000;

    /// <summary>Same ceiling as a payment: our price on top of the lab costs.</summary>
    public const long MaxAddon = MaxPaymentAmount;
}

/// <summary>Order status values. The desktop shows "OrderStatus.&lt;value&gt;".</summary>
public static class OrderStatuses
{
    public const string Pending = "Pending";
    public const string PartiallyPaid = "PartiallyPaid";
    public const string Paid = "Paid";
    public const string Cancelled = "Cancelled";
    public const string Refunded = "Refunded";

    public static readonly IReadOnlyList<string> All = [Pending, PartiallyPaid, Paid, Cancelled, Refunded];
}

/// <summary>Payment methods. The desktop shows "PaymentMethod.&lt;value&gt;".</summary>
public static class PaymentMethods
{
    public const string Cash = "Cash";
    public const string Card = "Card";
    public const string Transfer = "Transfer";
    public const string Insurance = "Insurance";

    public static readonly IReadOnlyList<string> All = [Cash, Card, Transfer, Insurance];
}

public record OrderListItemDto(
    Guid Id,
    string Number,
    DateTime CreatedAt,
    string PatientName,
    string PatientPhone,
    int TestsCount,
    long Total,
    long Paid,
    string Status);

/// <summary>How many orders are in each payment state (cancelled and refunded orders are not counted).</summary>
/// <param name="Unpaid">Nothing paid yet (status Pending).</param>
public record OrderStatsDto(int Paid, int PartiallyPaid, int Unpaid)
{
    public int Total => Paid + PartiallyPaid + Unpaid;
}

/// <summary>How many times a lab test was ordered (on orders that are not cancelled or refunded).</summary>
public record TopTestDto(Guid LabTestId, string Code, string Name, int Count);

/// <summary>
/// The Admin dashboard for one period (GET api/orders/dashboard). Money values are minor units of <see cref="Currency"/>.
/// For the period: Stats and Orders count orders created in it, Revenue is payments minus refunds made in it,
/// Outstanding is what its pending and partly paid orders still owe, RevenuePoints lists every bucket (zero included)
/// and TopTests the <see cref="OrderDashboardRules.TopTests"/> most ordered tests.
/// </summary>
public record OrderDashboardDto(
    string Currency,
    DashboardRangeDto Range,
    OrderStatsDto Stats,
    long Revenue,
    long Outstanding,
    IReadOnlyList<DashboardCountDto> Orders,
    IReadOnlyList<DashboardMoneyDto> RevenuePoints,
    IReadOnlyList<TopTestDto> TopTests);

public static class OrderDashboardRules
{
    /// <summary>How many of the most ordered tests the dashboard lists.</summary>
    public const int TopTests = 20;
}

/// <param name="LabCost">Lab cost snapshot at order time (what the lab charges the clinic).</param>
public record OrderItemDto(Guid LabTestId, string Code, string Name, long LabCost);

/// <summary>Payment types. The desktop shows "PaymentType.&lt;value&gt;".</summary>
public static class PaymentTypes
{
    public const string Payment = "Payment";
    public const string Refund = "Refund";
}

/// <param name="Type">One of <see cref="PaymentTypes"/>.</param>
/// <param name="Method">One of <see cref="PaymentMethods"/>.</param>
public record OrderPaymentDto(Guid Id, DateTime PaidAt, string Type, string Method, long Amount, string? Notes);

/// <param name="Addon">What the clinic added on top of the lab prices ("our price"). The patient total is this plus those prices.</param>
public record OrderDetailsDto(
    Guid Id,
    string Number,
    DateTime CreatedAt,
    string? CreatedBy,
    Guid PatientId,
    string PatientName,
    string PatientPhone,
    string Status,
    string Currency,
    long Total,
    long Paid,
    long Balance,
    long Addon,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<OrderPaymentDto> Payments);

/// <summary>
/// A new order. Exactly one of <see cref="PatientId"/> (an existing patient) and <see cref="NewPatient"/> (created with the order).
/// Field errors of nested values come back as "NewPatient.Phone", "Items[0].LabTestId", "FirstPayment.Amount".
/// </summary>
public record CreateOrderRequest
{
    public Guid? PatientId { get; init; }

    public CreatePatientRequest? NewPatient { get; init; }

    [Required(ErrorMessage = ErrorCodes.Required)]
    [MinLength(1, ErrorMessage = ErrorCodes.Required)]
    [MaxLength(OrderRules.MaxItems, ErrorMessage = ErrorCodes.MaxLength)]
    public IReadOnlyList<CreateOrderItemRequest> Items { get; init; } = [];

    /// <summary>What the clinic adds on top of the tests' lab costs ("our price"); null or 0 means no extra charge.</summary>
    [Range(0, OrderRules.MaxAddon, ErrorMessage = ErrorCodes.Range)]
    public long? Addon { get; init; }

    /// <summary>Optional; at most the order total.</summary>
    public CreateFirstPaymentRequest? FirstPayment { get; init; }
}

public record CreateOrderItemRequest
{
    [Required(ErrorMessage = ErrorCodes.Required)]
    public Guid? LabTestId { get; init; }
}

/// <summary>An additional payment on an existing order, in the order currency. At most what the order still owes.</summary>
public record AddPaymentRequest
{
    [Required(ErrorMessage = ErrorCodes.Required)]
    [Range(1, OrderRules.MaxPaymentAmount, ErrorMessage = ErrorCodes.Range)]
    public long? Amount { get; init; }

    [Required(ErrorMessage = ErrorCodes.Required)]
    [AllowedValues(PaymentMethods.Cash, PaymentMethods.Card, PaymentMethods.Transfer, PaymentMethods.Insurance, ErrorMessage = ErrorCodes.Invalid)]
    public string Method { get; init; } = PaymentMethods.Cash;

    [MaxLength(OrderRules.PaymentNotesMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string? Notes { get; init; }
}

/// <summary>Edits an existing payment; for a Payment-type row the amount may be at most the balance plus that row's current amount.</summary>
public record UpdatePaymentRequest
{
    [Required(ErrorMessage = ErrorCodes.Required)]
    [Range(1, OrderRules.MaxPaymentAmount, ErrorMessage = ErrorCodes.Range)]
    public long? Amount { get; init; }

    [Required(ErrorMessage = ErrorCodes.Required)]
    [AllowedValues(PaymentMethods.Cash, PaymentMethods.Card, PaymentMethods.Transfer, PaymentMethods.Insurance, ErrorMessage = ErrorCodes.Invalid)]
    public string Method { get; init; } = PaymentMethods.Cash;

    [MaxLength(OrderRules.PaymentNotesMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string? Notes { get; init; }
}

/// <summary>Paid in the clinic currency when the order is created.</summary>
public record CreateFirstPaymentRequest
{
    [Required(ErrorMessage = ErrorCodes.Required)]
    [Range(1, OrderRules.MaxPaymentAmount, ErrorMessage = ErrorCodes.Range)]
    public long? Amount { get; init; }

    [Required(ErrorMessage = ErrorCodes.Required)]
    [AllowedValues(PaymentMethods.Cash, PaymentMethods.Card, PaymentMethods.Transfer, PaymentMethods.Insurance, ErrorMessage = ErrorCodes.Invalid)]
    public string Method { get; init; } = PaymentMethods.Cash;

    [MaxLength(OrderRules.PaymentNotesMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string? Notes { get; init; }
}

/// <summary>Optional narrowing of the orders list (query string): one payment status and/or one patient's orders.</summary>
public record OrderListFilterRequest
{
    /// <summary>One of <see cref="OrderStatuses"/>; empty keeps every status.</summary>
    public string? Status { get; init; }

    public Guid? PatientId { get; init; }
}
