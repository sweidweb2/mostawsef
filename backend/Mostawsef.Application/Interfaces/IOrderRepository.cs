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

public record TopTestRow(Guid LabTestId, string Code, string Name, int Count);

public record OrderPaymentRow(DateTime PaidAt, PaymentType Type, PaymentMethod Method, long Amount, string? Notes);

/// <summary>Optional narrowing of the orders list: one status and/or one patient's orders.</summary>
public record OrderListFilter(OrderStatus? Status, Guid? PatientId);

public interface IOrderRepository
{
    /// <summary>Details projection: items + lab-test labels and the payments, oldest first.</summary>
    Task<OrderDetailsRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Newest first. Search is LIKE on SearchText (trigram; name, phone, number); <paramref name="filter"/> narrows by status and patient.</summary>
    Task<(IReadOnlyList<OrderListRow> Items, int Total)> GetPageAsync(
        PageQuery query, OrderListFilter filter, CancellationToken cancellationToken);

    /// <summary>Order counts per status (statuses without orders are absent).</summary>
    Task<IReadOnlyDictionary<OrderStatus, int>> GetStatusCountsAsync(CancellationToken cancellationToken);

    /// <summary>When each order created at or after <paramref name="sinceUtc"/> was created (UTC), oldest first.</summary>
    Task<IReadOnlyList<DateTime>> GetCreatedSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken);

    /// <summary>The most ordered tests, most first (ties by name): order lines of cancelled and refunded orders are not counted.</summary>
    Task<IReadOnlyList<TopTestRow>> GetTopTestsAsync(int take, CancellationToken cancellationToken);

    /// <summary>The tracked order with its items and payments, to add a payment and answer with the updated order; null when it does not exist.</summary>
    Task<Order?> GetForPaymentAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Marks a payment added to an already saved order as new (its id is set up front, so it is not found by tracking).</summary>
    void AddPayment(Payment payment);

    void Add(Order order);

    /// <summary>Saves every change of the request's unit of work (e.g. a new patient and its order) in one transaction.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
