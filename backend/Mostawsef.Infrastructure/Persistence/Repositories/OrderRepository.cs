using System.Data;
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
                o.Total,
                o.Paid,
                o.Total - o.Paid,
                o.Addon,
                o.Items.Select(i => new OrderItemRow(
                    i.LabTestId,
                    i.LabTestCode,
                    i.LabTestName,
                    i.LabPrice)).ToList(),
                o.Payments.OrderBy(p => p.PaidAt).ThenBy(p => p.Id).Select(p => new OrderPaymentRow(
                    p.Id,
                    p.PaidAt,
                    p.Type,
                    p.Method,
                    p.Amount,
                    p.Notes)).ToList(),
                o.Version))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<OrderListRow> Items, int Total)> GetPageAsync(
        PageQuery query, OrderListFilter filter, CancellationToken cancellationToken)
    {
        var orders = db.Orders.AsNoTracking();

        if (filter.PatientId is { } patient)
        {
            orders = orders.Where(o => o.PatientId == patient);
        }

        if (filter.Status is { } wanted)
        {
            orders = orders.Where(o => o.Status == wanted);
        }

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
                o.TestsCount,
                o.Total,
                o.Paid,
                o.Status,
                o.Currency));

        return await rows.ToPageAsync(orders, query, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<OrderStatus, int>> GetStatusCountsAsync(
        DateTime sinceUtc, DateTime untilUtc, CancellationToken cancellationToken) =>
        (await db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= sinceUtc && o.CreatedAt < untilUtc)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken))
        .ToDictionary(g => g.Status, g => g.Count);

    public Task<IReadOnlyList<HourlyValue>> GetCreatedPerHourAsync(DateTime sinceUtc, DateTime untilUtc, CancellationToken cancellationToken) =>
        db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= sinceUtc && o.CreatedAt < untilUtc)
            .Select(o => o.CreatedAt)
            .CountPerHourAsync(cancellationToken);

    public Task<long> GetOutstandingAsync(DateTime sinceUtc, DateTime untilUtc, CancellationToken cancellationToken) =>
        db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= sinceUtc && o.CreatedAt < untilUtc
                && (o.Status == OrderStatus.Pending || o.Status == OrderStatus.PartiallyPaid))
            .SumAsync(o => o.Total > o.Paid ? o.Total - o.Paid : 0L, cancellationToken);

    public async Task<IReadOnlyList<HourlyValue>> GetRevenuePerHourAsync(DateTime sinceUtc, DateTime untilUtc, CancellationToken cancellationToken) =>
        HourlySeries.ToHours(await db.Set<Payment>().AsNoTracking()
            .Where(p => p.PaidAt >= sinceUtc && p.PaidAt < untilUtc)
            .GroupBy(p => new { p.PaidAt.Year, p.PaidAt.Month, p.PaidAt.Day, p.PaidAt.Hour })
            .Select(g => new HourlySeries.HourRow(
                g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour,
                g.Sum(p => p.Type == PaymentType.Refund ? -p.Amount : p.Amount)))
            .ToListAsync(cancellationToken));

    public Task<DateTime?> GetFirstCreatedAtAsync(CancellationToken cancellationToken) =>
        db.Orders.AsNoTracking().MinAsync(o => (DateTime?)o.CreatedAt, cancellationToken);

    public async Task<IReadOnlyList<TopTestRow>> GetTopTestsAsync(
        int take, DateTime sinceUtc, DateTime untilUtc, CancellationToken cancellationToken)
    {
        var counted = from item in db.OrderItems.AsNoTracking()
                      join order in db.Orders.AsNoTracking() on item.OrderId equals order.Id
                      where order.CreatedAt >= sinceUtc && order.CreatedAt < untilUtc
                            && order.Status != OrderStatus.Cancelled && order.Status != OrderStatus.Refunded
                      group item by item.LabTestId into g
                      select new { Id = g.Key, Code = g.Max(i => i.LabTestCode), Name = g.Max(i => i.LabTestName), Count = g.Count() };

        var rows = await counted
            .OrderByDescending(r => r.Count)
            .ThenBy(r => r.Name)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows.Select(r => new TopTestRow(r.Id, r.Code ?? string.Empty, r.Name ?? string.Empty, r.Count)).ToList();
    }

    public Task<Order?> GetForPaymentAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders.Include(o => o.Items).Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public void AddPayment(Payment payment) => db.Set<Payment>().Add(payment);

    public void RemovePayment(Payment payment) => db.Set<Payment>().Remove(payment);

    public void Add(Order order) => db.Orders.Add(order);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public Task<Order?> FindByClientRequestIdAsync(Guid clientRequestId, CancellationToken cancellationToken) =>
        db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.ClientRequestId == clientRequestId, cancellationToken);

    public Task<Payment?> FindPaymentByClientRequestIdAsync(Guid clientRequestId, CancellationToken cancellationToken) =>
        db.Set<Payment>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.ClientRequestId == clientRequestId, cancellationToken);

    /// <summary>
    /// How often a transaction that lost a serialization race is run again before the error reaches the client.
    /// Every order create takes from the same number row, so each round of simultaneous creates lets one commit:
    /// this covers far more simultaneous saves than one clinic's PCs make.
    /// </summary>
    internal const int SerializableAttempts = 10;

    public async Task ExecuteInSerializableAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                await action(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < SerializableAttempts && AppDbContext.IsSerializationFailure(ex))
            {
                // Two saves touched the same rows (e.g. both took an order number): PostgreSQL rolled this one back.
                // Drop what the failed attempt added and run it again; the action rebuilds everything it saves.
                db.ChangeTracker.Clear();
                // Random spread so the losers of one round do not collide again in the next.
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(5, 25) * attempt), cancellationToken);
            }
        }
    }
}
