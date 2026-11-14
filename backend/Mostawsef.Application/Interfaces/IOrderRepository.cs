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

    void Add(Order order);

    /// <summary>Saves every change of the request's unit of work (e.g. a new patient and its order) in one transaction.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
