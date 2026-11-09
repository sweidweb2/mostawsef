using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class OrderRepository(AppDbContext db) : IOrderRepository
{
    public Task<OrderDetailsRow?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new OrderDetailsRow(
                o.Id,
                o.Number,
                o.CreatedAt,
                o.CreatedBy,
                o.PatientId,
                o.PatientName,
                o.PatientPhone,
                o.Status,
                o.Currency,
                o.OurPrice,
                o.Payments.Sum(p => p.Type == PaymentType.Refund ? -p.Amount : p.Amount),
                o.Items.Select(i => new OrderItemRow(
                    i.LabTestId,
                    i.LabTest != null ? i.LabTest.Code : string.Empty,
                    i.LabTest != null ? i.LabTest.Name : string.Empty,
                    i.OurPrice)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<OrderListRow> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var orders = db.Orders.AsNoTracking();

        if (TrigramSearch.ContainsPattern(query.SafeSearch) is { } pattern)
        {
            orders = orders.Where(o => EF.Functions.Like(o.SearchText, pattern, TrigramSearch.EscapeCharacter));
        }

        var rows = orders
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Select(o => new OrderListRow(
                o.Id,
                o.Number,
                o.CreatedAt,
                o.PatientName,
                o.PatientPhone,
                o.Items.Count,
                o.OurPrice,
                o.Payments.Sum(p => p.Type == PaymentType.Refund ? -p.Amount : p.Amount),
                o.Status));

        return await rows.ToPageAsync(query, cancellationToken);
    }

    public void Add(Order order) => db.Orders.Add(order);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
