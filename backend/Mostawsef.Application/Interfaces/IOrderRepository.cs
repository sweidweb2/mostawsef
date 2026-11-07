using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Interfaces;

public interface IOrderRepository
{
    /// <summary>With its items (and their lab tests) and payments.</summary>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Newest first, with items and payments. Search matches the patient's normalized name, the phone or the number.</summary>
    Task<(IReadOnlyList<Order> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    void Add(Order order);

    /// <summary>Saves every change of the request's unit of work (e.g. a new patient and its order) in one transaction.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
