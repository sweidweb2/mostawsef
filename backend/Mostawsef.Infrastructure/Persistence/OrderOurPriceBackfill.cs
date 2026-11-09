using Microsoft.EntityFrameworkCore;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence;

/// <summary>
/// Fills orders.OurPrice for rows created before AddOrderOurPrice (the column starts at 0).
/// Plain EF Core instead of SQL in the migration. DatabaseInitializer runs it once after that migration.
/// </summary>
public static class OrderOurPriceBackfill
{
    public const string Migration = "20261109200541_AddOrderOurPrice";

    public const int BatchSize = 500;

    /// <summary>Returns how many orders were updated.</summary>
    public static async Task<int> RunAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var updated = 0;
        Guid? lastId = null;
        while (true)
        {
            var query = db.Orders.Include(o => o.Items).Where(o => o.OurPrice == 0);
            if (lastId is { } after)
            {
                query = query.Where(o => o.Id.CompareTo(after) > 0);
            }

            var batch = await query.OrderBy(o => o.Id).Take(BatchSize).ToListAsync(cancellationToken);
            foreach (var order in batch)
            {
                order.SetOurPrice(order.CatalogTotal);
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
