using Microsoft.EntityFrameworkCore;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence;

/// <summary>
/// Copies live lab-test code/name onto order_items rows created before the snapshot columns existed.
/// Plain EF Core (no raw SQL). DatabaseInitializer runs it once after that migration.
/// </summary>
public static class OrderItemSnapshotBackfill
{
    public const string Migration = "20261111220000_AddOrderItemLabTestSnapshot";

    public const int BatchSize = 500;

    /// <summary>Returns how many order items were updated.</summary>
    public static async Task<int> RunAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var updated = 0;
        Guid? lastId = null;
        while (true)
        {
            // IgnoreQueryFilters so soft-deleted lab tests still fill the snapshot.
            var query = db.Set<OrderItem>()
                .IgnoreQueryFilters()
                .Include(i => i.LabTest)
                .Where(i => i.LabTestCode == string.Empty || i.LabTestName == string.Empty);
            if (lastId is { } after)
            {
                query = query.Where(i => i.Id.CompareTo(after) > 0);
            }

            var batch = await query.OrderBy(i => i.Id).Take(BatchSize).ToListAsync(cancellationToken);
            foreach (var item in batch)
            {
                if (item.LabTest is null)
                {
                    continue;
                }

                db.Entry(item).Property(i => i.LabTestCode).CurrentValue = item.LabTest.Code;
                db.Entry(item).Property(i => i.LabTestName).CurrentValue = item.LabTest.Name;
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
