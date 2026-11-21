using Microsoft.EntityFrameworkCore;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence;

/// <summary>
/// Copies orders.Total into orders.Addon for rows created when the two were the same number.
/// The saved total is left alone, so an order already charged is not recalculated.
/// Plain EF Core instead of SQL in the migration. DatabaseInitializer runs it once after AddOrderAddon.
/// </summary>
public static class OrderAddonBackfill
{
    public const string Migration = "20261121092802_AddOrderAddon";

    public const int BatchSize = 500;

    /// <summary>Returns how many orders were updated.</summary>
    public static async Task<int> RunAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var updated = 0;
        Guid? lastId = null;
        while (true)
        {
            var query = db.Orders.Where(o => o.Addon == 0 && o.Total != 0);
            if (lastId is { } after)
            {
                query = query.Where(o => o.Id.CompareTo(after) > 0);
            }

            var batch = await query.OrderBy(o => o.Id).Take(BatchSize).ToListAsync(cancellationToken);
            foreach (var order in batch)
            {
                order.KeepSavedTotalAsAddon();
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
