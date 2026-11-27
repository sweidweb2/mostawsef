using Mostawsef.Domain.Common;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Domain.Entities;

/// <summary>Money received for (or refunded from) an order. Edits and deletes recalculate the order's Paid and status.</summary>
public class Payment : Entity
{
    public const int NotesMaxLength = 500;

    private Payment()
    {
    }

    internal Payment(PaymentType type, PaymentMethod method, string currency, long amount, decimal exchangeRate, DateTime paidAt, string? notes)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be positive.");
        }

        Type = type;
        Method = method;
        Currency = currency;
        Amount = amount;
        ExchangeRate = exchangeRate;
        PaidAt = paidAt;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public Guid OrderId { get; private set; }

    public PaymentType Type { get; private set; }

    public PaymentMethod Method { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    /// <summary>Positive, minor units of <see cref="Currency"/>.</summary>
    public long Amount { get; private set; }

    /// <summary>Currency to order currency.</summary>
    public decimal ExchangeRate { get; private set; }

    public DateTime PaidAt { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>Optional client save-attempt id; unique when set so a timed-out retry returns the same payment.</summary>
    public Guid? ClientRequestId { get; private set; }

    internal void SetClientRequestId(Guid? clientRequestId) => ClientRequestId = clientRequestId;

    /// <summary>Changes method, amount and notes; <see cref="PaidAt"/> stays the original payment time.</summary>
    internal void Update(PaymentMethod method, long amount, string? notes)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be positive.");
        }

        Method = method;
        Amount = amount;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}
