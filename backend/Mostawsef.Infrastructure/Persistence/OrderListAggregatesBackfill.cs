using Microsoft.EntityFrameworkCore;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence;

/// <summary>
/// Fills orders.TestsCount and orders.Paid for rows created before AddOrderListAggregates (columns start at 0).
/// Plain EF Core instead of SQL in the migration. DatabaseInitializer runs it once after that migration.
/// </summary>
public static class OrderListAggregatesBackfill
{
    public const string Migration = "20261109215316_AddOrderListAggregates";

    public const int BatchSize = 500;

    /// <summary>Returns how many orders were updated.</summary>
    public static async Task<int> RunAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var updated = 0;
        Guid? lastId = null;
        while (true)
        {
            // SplitQuery avoids Items × Payments row multiplication on the join.
            var query = db.Orders.Include(o => o.Items).Include(o => o.Payments).AsSplitQuery().AsQueryable();
            if (lastId is { } after)
            {
                query = query.Where(o => o.Id.CompareTo(after) > 0);
            }

            var batch = await query.OrderBy(o => o.Id).Take(BatchSize).ToListAsync(cancellationToken);
            foreach (var order in batch)
            {
                var paid = order.Payments.Sum(p => p.Type == PaymentType.Refund ? -p.Amount : p.Amount);
                order.SetListAggregates(order.Items.Count, paid);
            }

            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
            updated += batch.Count;

            if (batch.Count < BatchSize)
            {
                return updated;
            }

            lastId = batch[^1].Id;
        }
    }
}
