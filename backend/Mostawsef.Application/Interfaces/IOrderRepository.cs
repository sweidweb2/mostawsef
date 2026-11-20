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

public record OrderItemRow(Guid LabTestId, string Code, string Name, long LabCost);

public record TopTestRow(Guid LabTestId, string Code, string Name, int Count);

public record OrderPaymentRow(Guid Id, DateTime PaidAt, PaymentType Type, PaymentMethod Method, long Amount, string? Notes);

/// <summary>Optional narrowing of the orders list: one status and/or one patient's orders.</summary>
public record OrderListFilter(OrderStatus? Status, Guid? PatientId);

public interface IOrderRepository
{
    /// <summary>Details projection: items + lab-test labels and the payments, oldest first.</summary>
    Task<OrderDetailsRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Newest first. Search is LIKE on SearchText (trigram; name, phone, number); <paramref name="filter"/> narrows by status and patient.</summary>
    Task<(IReadOnlyList<OrderListRow> Items, int Total)> GetPageAsync(
        PageQuery query, OrderListFilter filter, CancellationToken cancellationToken);

    /// <summary>Counts per status of the orders created in [<paramref name="sinceUtc"/>, <paramref name="untilUtc"/>) (statuses without orders are absent).</summary>
    Task<IReadOnlyDictionary<OrderStatus, int>> GetStatusCountsAsync(DateTime sinceUtc, DateTime untilUtc, CancellationToken cancellationToken);

    /// <summary>Orders created per UTC hour in [<paramref name="sinceUtc"/>, <paramref name="untilUtc"/>); hours without orders are absent.</summary>
    Task<IReadOnlyList<HourlyValue>> GetCreatedPerHourAsync(DateTime sinceUtc, DateTime untilUtc, CancellationToken cancellationToken);

    /// <summary>
    /// What pending and partly paid orders created in [<paramref name="sinceUtc"/>, <paramref name="untilUtc"/>) still owe
    /// (Total − Paid), in minor units.
    /// </summary>
    Task<long> GetOutstandingAsync(DateTime sinceUtc, DateTime untilUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Net payments per UTC hour in [<paramref name="sinceUtc"/>, <paramref name="untilUtc"/>) (payments minus refunds by
    /// PaidAt); hours without payments are absent. Values are minor units (refunds are negative).
    /// </summary>
    Task<IReadOnlyList<HourlyValue>> GetRevenuePerHourAsync(DateTime sinceUtc, DateTime untilUtc, CancellationToken cancellationToken);

    /// <summary>
    /// The most ordered tests on orders created in [<paramref name="sinceUtc"/>, <paramref name="untilUtc"/>), most first
    /// (ties by name): order lines of cancelled and refunded orders are not counted.
    /// </summary>
    Task<IReadOnlyList<TopTestRow>> GetTopTestsAsync(int take, DateTime sinceUtc, DateTime untilUtc, CancellationToken cancellationToken);

    /// <summary>When the oldest order was created (UTC); null when there is none.</summary>
    Task<DateTime?> GetFirstCreatedAtAsync(CancellationToken cancellationToken);

    /// <summary>The tracked order with its items and payments, to add a payment and answer with the updated order; null when it does not exist.</summary>
    Task<Order?> GetForPaymentAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Marks a payment added to an already saved order as new (its id is set up front, so it is not found by tracking).</summary>
    void AddPayment(Payment payment);

    /// <summary>Marks a payment removed from an order for hard delete.</summary>
    void RemovePayment(Payment payment);

    void Add(Order order);

    /// <summary>Saves every change of the request's unit of work (e.g. a new patient and its order) in one transaction.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
