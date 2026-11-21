using Microsoft.EntityFrameworkCore;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence;

/// <summary>
/// Copies orders.Total into orders.Addon for legacy rows created when the two were the same number.
/// The saved total is left alone, so an order already charged is not recalculated.
/// Skips new-model rows where Addon is 0 and Total already equals the snapshotted lab sum.
/// Uses <c>ExecuteUpdate</c> (no Version bump) and a race-safe completion marker.
/// </summary>
public static class OrderAddonBackfill
{
    public const string Migration = "20261121092802_AddOrderAddon";

    /// <summary>Completion marker in <c>number_sequences</c> (not a real document counter).</summary>
    public const string CompletionMarker = "backfill.order-addon";

    /// <summary>True when the Addon migration is applied but this backfill has not finished.</summary>
    public static async Task<bool> ShouldRunAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken);
        if (!applied.Contains(Migration))
        {
            return false;
        }

        return !await DataBackfillMarker.ExistsAsync(db, CompletionMarker, cancellationToken);
    }

    /// <summary>Returns how many orders were updated.</summary>
    public static async Task<int> RunAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        // Legacy unfinished: Total was our price alone (not labs + addon). New Addon=0 orders have Total == Σ LabPrice.
        var updated = await db.Orders
            .Where(o => o.Addon == 0 && o.Total != 0 && o.Total != o.Items.Sum(i => i.LabPrice))
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Addon, o => o.Total), cancellationToken);

        await DataBackfillMarker.EnsureAsync(db, CompletionMarker, cancellationToken);
        return updated;
    }
}
