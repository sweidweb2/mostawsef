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
    public const int SearchTextMaxLength =
        Patient.FullNameMaxLength + Patient.PhoneMaxLength + NumberMaxLength + 2;

    private readonly List<OrderItem> _items = [];
    private readonly List<Payment> _payments = [];

    private Order()
    {
    }

    public Order(string number, Patient patient, string currency)
        : this(number, patient.Id, patient.FullName, patient.SearchName, patient.Phone, currency)
    {
    }

    public Order(string number, Guid patientId, string patientName, string patientSearchName, string patientPhone, string currency)
    {
        Number = number;
        PatientId = patientId;
        PatientName = patientName;
        PatientSearchName = patientSearchName;
        PatientPhone = patientPhone;
        Currency = currency;
    }

    /// <summary>Readable number, e.g. ORD-000123. Unique.</summary>
    public string Number { get; private set; } = string.Empty;

    public Guid PatientId { get; private set; }

    /// <summary>The patient's name when the order was made, kept as on the invoice: renaming the patient later does not change it.</summary>
    public string PatientName { get; private set; } = string.Empty;

    /// <summary>PatientName normalized for search.</summary>
    public string PatientSearchName { get; private set; } = string.Empty;

    public string PatientPhone { get; private set; } = string.Empty;

    /// <summary>Patient search name, phone and number, normalized (OrderService). What the list search matches.</summary>
    public string SearchText { get; private set; } = string.Empty;

    /// <summary>Set from the payments by <see cref="UpdateStatus"/>.</summary>
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    public DiscountType? DiscountType { get; private set; }

    /// <summary>Not used yet: stays 0.</summary>
    public long DiscountValue { get; private set; }

    /// <summary>What the patient is charged: our price plus the lab prices copied onto the items (minor units).</summary>
    public long Total { get; private set; }

    /// <summary>What the clinic adds on top of the lab prices (minor units). Stored apart from <see cref="Total"/>.</summary>
    public long Addon { get; private set; }

    /// <summary>How many tests are on the order. Kept in sync by <see cref="AddItem"/> for the list page.</summary>
    public int TestsCount { get; private set; }

    /// <summary>Payments minus refunds (minor units). Kept in sync by <see cref="AddPayment"/> for the list page.</summary>
    public long Paid { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public string? Notes { get; private set; }

    /// <summary>Optional client save-attempt id; unique when set so a timed-out retry returns this order.</summary>
    public Guid? ClientRequestId { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;

    public IReadOnlyList<Payment> Payments => _payments;

    public long Balance => Total - Paid;

    public void AssignNumber(string number) => Number = number;

    public void SetClientRequestId(Guid? clientRequestId) => ClientRequestId = clientRequestId;

    public void AddItem(LabTest test)
    {
        _items.Add(new OrderItem(test));
        TestsCount = _items.Count;
    }

    /// <summary>Sets our price and the patient total from the items already added. Call after <see cref="AddItem"/>.</summary>
    public void SetCharges(long addon)
    {
        if (addon < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(addon), "Addon cannot be negative.");
        }

        Addon = addon;
        Total = addon + _items.Sum(i => i.LabPrice);
    }

    /// <summary>Sets the chargeable total. Used by the one-time backfill of orders saved before our price existed.</summary>
    public void SetTotal(long total)
    {
        if (total < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(total), "Total cannot be negative.");
        }

        Total = total;
    }

    /// <summary>A payment in the order currency (exchange rate 1). At most <see cref="Balance"/>.</summary>
    public Payment AddPayment(PaymentMethod method, long amount, DateTime paidAt, string? notes, Guid? clientRequestId = null)
    {
        if (amount > Balance)
        {
            throw new InvalidOperationException("Payment exceeds the order balance.");
        }

        var payment = new Payment(PaymentType.Payment, method, Currency, amount, 1m, paidAt, notes);
        payment.SetClientRequestId(clientRequestId);
        _payments.Add(payment);
        Paid = SumPayments();
        return payment;
    }

    /// <summary>Updates an existing payment and refreshes <see cref="Paid"/>. Payment rows stay within the order balance.</summary>
    public void UpdatePayment(Payment payment, PaymentMethod method, long amount, string? notes)
    {
        if (payment.Type == PaymentType.Payment && amount > Balance + payment.Amount)
        {
            throw new InvalidOperationException("Payment exceeds the order balance.");
        }

        payment.Update(method, amount, notes);
        Paid = SumPayments();
    }

    /// <summary>Removes a payment and refreshes <see cref="Paid"/>.</summary>
    public void RemovePayment(Payment payment)
    {
        _payments.Remove(payment);
        Paid = SumPayments();
    }

    /// <summary>
    /// Paid once nothing is owed, PartiallyPaid after a first payment, Pending before.
    /// Cancelled and Refunded are reserved for a future workflow and are never assigned here.
    /// </summary>
    public void UpdateStatus() =>
        Status = Paid >= Total ? OrderStatus.Paid
            : Paid > 0 ? OrderStatus.PartiallyPaid
            : OrderStatus.Pending;

    public void SetSearchText(string searchText) => SearchText = searchText;

    /// <summary>Used by the one-time data backfill after TestsCount/Paid columns were added.</summary>
    public void SetListAggregates(int testsCount, long paid)
    {
        TestsCount = testsCount;
        Paid = paid;
    }

    private long SumPayments() =>
        _payments.Sum(p => p.Type == PaymentType.Refund ? -p.Amount : p.Amount);
}
