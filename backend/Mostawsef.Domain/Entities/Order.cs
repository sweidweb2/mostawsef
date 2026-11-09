using Mostawsef.Domain.Common;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Domain.Entities;

/// <summary>
/// Tests ordered for a patient, with the payments made against them. Amounts are minor units of <see cref="Currency"/>.
/// The patient's name and phone are copied when the order is created, so later edits to the patient do not change it.
/// </summary>
public class Order : Entity
{
    public const int NumberMaxLength = 20;
    public const int CurrencyMaxLength = 3;
    public const int NotesMaxLength = 2000;

    private readonly List<OrderItem> _items = [];
    private readonly List<Payment> _payments = [];

    private Order()
    {
    }

    public Order(string number, Patient patient, string currency)
    {
        Number = number;
        PatientId = patient.Id;
        PatientName = patient.FullName;
        PatientSearchName = patient.SearchName;
        PatientPhone = patient.Phone;
        Currency = currency;
    }

    /// <summary>Readable number, e.g. ORD-000123. Unique.</summary>
    public string Number { get; private set; } = string.Empty;

    public Guid PatientId { get; private set; }

    public string PatientName { get; private set; } = string.Empty;

    /// <summary>PatientName normalized for search.</summary>
    public string PatientSearchName { get; private set; } = string.Empty;

    public string PatientPhone { get; private set; } = string.Empty;

    /// <summary>Set from the payments by <see cref="UpdateStatus"/>.</summary>
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    public DiscountType? DiscountType { get; private set; }

    /// <summary>Not used yet: stays 0.</summary>
    public long DiscountValue { get; private set; }

    /// <summary>What the patient is charged for the whole order (minor units).</summary>
    public long OurPrice { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public string? Notes { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;

    public IReadOnlyList<Payment> Payments => _payments;

    /// <summary>Sum of the items' catalog prices (before an order-level our-price override).</summary>
    public long CatalogTotal => _items.Sum(i => i.OurPrice);

    /// <summary>What the patient owes (no discounts yet).</summary>
    public long Total => OurPrice;

    /// <summary>Payments minus refunds.</summary>
    public long Paid => _payments.Sum(p => p.Type == PaymentType.Refund ? -p.Amount : p.Amount);

    public long Balance => Total - Paid;

    public void AddItem(LabTest test) => _items.Add(new OrderItem(test));

    /// <summary>Sets the chargeable total. Call after the items are added.</summary>
    public void SetOurPrice(long ourPrice) => OurPrice = ourPrice;

    /// <summary>A payment in the order currency (exchange rate 1).</summary>
    public void AddPayment(PaymentMethod method, long amount, DateTime paidAt, string? notes) =>
        _payments.Add(new Payment(PaymentType.Payment, method, Currency, amount, 1m, paidAt, notes));

    /// <summary>Paid once nothing is owed, PartiallyPaid after a first payment, Pending before.</summary>
    public void UpdateStatus() =>
        Status = Paid >= Total ? OrderStatus.Paid
            : Paid > 0 ? OrderStatus.PartiallyPaid
            : OrderStatus.Pending;
}
