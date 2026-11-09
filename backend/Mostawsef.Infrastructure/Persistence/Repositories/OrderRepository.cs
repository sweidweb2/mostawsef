using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class OrderRepository(AppDbContext db) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders
            .Include(o => o.Items).ThenInclude(i => i.LabTest)
            .Include(o => o.Payments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<OrderListRow> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var orders = db.Orders.AsNoTracking();

        var term = SearchText.Normalize(query.SafeSearch);
        if (term.Length > 0)
        {
            orders = orders.Where(o => o.SearchText.Contains(term));
        }

        var total = query.IncludeTotal
            ? await orders.CountAsync(cancellationToken)
            : PageQuery.TotalNotComputed;
        var items = await orders
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .Select(o => new OrderListRow(
                o.Id,
                o.Number,
                o.CreatedAt,
                o.PatientName,
                o.PatientPhone,
                o.Items.Count,
                o.OurPrice,
                o.Payments.Sum(p => p.Type == PaymentType.Refund ? -p.Amount : p.Amount),
                o.Status))
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public void Add(Order order) => db.Orders.Add(order);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
