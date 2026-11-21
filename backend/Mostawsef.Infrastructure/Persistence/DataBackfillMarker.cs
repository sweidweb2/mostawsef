using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Common;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence;

/// <summary>
/// Completion rows in <c>number_sequences</c> for one-time data backfills. Insert is safe when two API
/// processes start together (the loser's unique race is ignored).
/// </summary>
internal static class DataBackfillMarker
{
    public static Task<bool> ExistsAsync(AppDbContext db, string name, CancellationToken cancellationToken) =>
        db.Set<NumberSequence>().AnyAsync(s => s.Name == name, cancellationToken);

    public static async Task EnsureAsync(AppDbContext db, string name, CancellationToken cancellationToken)
    {
        if (await ExistsAsync(db, name, cancellationToken))
        {
            return;
        }

        try
        {
            db.Add(new NumberSequence(name, 1));
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (AppException ex) when (ex.Code == ErrorCodes.Taken)
        {
            // Another host inserted the marker first.
            db.ChangeTracker.Clear();
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
        }
    }
}
