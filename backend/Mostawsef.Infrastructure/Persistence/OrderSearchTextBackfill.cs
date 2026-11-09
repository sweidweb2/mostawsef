using Microsoft.EntityFrameworkCore;
using Mostawsef.Contracts.Common;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence;

/// <summary>
/// Fills orders.SearchText for rows created before AddOrderSearchText (the column starts empty).
/// Plain EF Core instead of SQL in the migration. DatabaseInitializer runs it once after that migration.
/// </summary>
public static class OrderSearchTextBackfill
{
    public const string Migration = "20261109201524_AddOrderSearchText";

    public const int BatchSize = 500;

    /// <summary>Returns how many orders were updated.</summary>
    public static async Task<int> RunAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var updated = 0;
        Guid? lastId = null;
        while (true)
        {
            var query = db.Orders.Where(o => o.SearchText == string.Empty);
            if (lastId is { } after)
            {
                query = query.Where(o => o.Id.CompareTo(after) > 0);
            }

            var batch = await query.OrderBy(o => o.Id).Take(BatchSize).ToListAsync(cancellationToken);
            foreach (var order in batch)
            {
                order.SetSearchText(SearchText.Key(order.PatientSearchName, order.PatientPhone, order.Number));
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
