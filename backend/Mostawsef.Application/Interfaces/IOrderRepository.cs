using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Interfaces;

/// <summary>One row of the orders list: no item or payment entities loaded.</summary>
public record OrderListRow(
    Guid Id,
    string Number,
    DateTime CreatedAt,
    string PatientName,
    string PatientPhone,
    int TestsCount,
    long Total,
    long Paid,
    OrderStatus Status);

/// <summary>Order details projection; Paid and TestsCount are stored on the order.</summary>
public record OrderDetailsRow(
    Guid Id,
    string Number,
    DateTime CreatedAt,
    string? CreatedBy,
    Guid PatientId,
    string PatientName,
    string PatientPhone,
    OrderStatus Status,
    string Currency,
    long Total,
    long Paid,
    long Balance,
    long Addon,
    IReadOnlyList<OrderItemRow> Items,
    IReadOnlyList<OrderPaymentRow> Payments);

public record OrderItemRow(Guid LabTestId, string Code, string Name, long Price);

public record OrderPaymentRow(DateTime PaidAt, PaymentType Type, PaymentMethod Method, long Amount, string? Notes);

public interface IOrderRepository
{
    /// <summary>Details projection: items + lab-test labels and the payments, oldest first.</summary>
    Task<OrderDetailsRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Newest first. Search is LIKE on SearchText (trigram; name, phone, number).</summary>
    Task<(IReadOnlyList<OrderListRow> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    /// <summary>Order counts per status (statuses without orders are absent).</summary>
    Task<IReadOnlyDictionary<OrderStatus, int>> GetStatusCountsAsync(CancellationToken cancellationToken);

    /// <summary>The tracked order with its payments, to add a payment; null when it does not exist.</summary>
    Task<Order?> GetWithPaymentsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Marks a payment added to an already saved order as new (its id is set up front, so it is not found by tracking).</summary>
    void AddPayment(Payment payment);

    void Add(Order order);

    /// <summary>Saves every change of the request's unit of work (e.g. a new patient and its order) in one transaction.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
