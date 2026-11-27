using Mostawsef.Contracts.Common;

namespace Mostawsef.Contracts.Orders;

/// <summary>
/// Maps the shared amount / method / notes fields used by New Order's first payment and by
/// add/edit payment forms into the matching Contracts request types.
/// </summary>
public static class OrderPaymentMapping
{
    public static string? NormalizeNotes(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    public static long? ToMinorAmount(decimal? amount) =>
        amount is { } value ? Money.ToMinor(value) : null;

    /// <summary>Null when no amount was entered (optional first payment on create).</summary>
    public static CreateFirstPaymentRequest? ToFirstPayment(decimal? amount, string method, string? notes) =>
        amount is null
            ? null
            : new CreateFirstPaymentRequest
            {
                Amount = Money.ToMinor(amount.Value),
                Method = method,
                Notes = NormalizeNotes(notes),
            };

    public static AddPaymentRequest ToAdd(
        decimal? amount, string method, string? notes, int orderVersion, Guid? clientRequestId = null) =>
        new()
        {
            Amount = ToMinorAmount(amount),
            Method = method,
            Notes = NormalizeNotes(notes),
            Version = orderVersion,
            ClientRequestId = clientRequestId,
        };

    public static UpdatePaymentRequest ToUpdate(decimal? amount, string method, string? notes, int orderVersion) =>
        new()
        {
            Amount = ToMinorAmount(amount),
            Method = method,
            Notes = NormalizeNotes(notes),
            Version = orderVersion,
        };
}
