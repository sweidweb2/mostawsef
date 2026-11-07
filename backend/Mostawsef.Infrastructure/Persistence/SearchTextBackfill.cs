using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Mostawsef.Domain.Common;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence;

/// <summary>
/// Fills SearchText for log rows written before the LogSearchText migration added it (the column starts empty).
/// Plain EF Core instead of SQL in the migration, so it stays portable and uses the same normalization as new rows.
/// DatabaseInitializer runs it once, right after that migration is applied.
/// </summary>
public static class SearchTextBackfill
{
    public const string Migration = "20261107153036_LogSearchText";

    public const int BatchSize = 1000;

    /// <summary>Returns how many rows were rebuilt.</summary>
    public static async Task<int> RunAsync(AppDbContext db, CancellationToken cancellationToken = default) =>
        await FillAsync(db, db.ActivityLogs, l => l.SearchText == string.Empty, l => l.RebuildSearchText(), cancellationToken)
        + await FillAsync(db, db.SystemErrors, e => e.SearchText == string.Empty, e => e.RebuildSearchText(), cancellationToken);

    /// <summary>Walks the empty rows in Id order, one batch per SaveChanges; a row whose fields are all empty stays empty and is not read again.</summary>
    private static async Task<int> FillAsync<T>(
        AppDbContext db,
        DbSet<T> set,
        Expression<Func<T, bool>> isEmpty,
        Action<T> rebuild,
        CancellationToken cancellationToken)
        where T : class, IChronological
    {
        var rebuilt = 0;
        Guid? lastId = null;
        while (true)
        {
            var rows = set.Where(isEmpty);
            if (lastId is { } after)
            {
                rows = rows.Where(r => r.Id.CompareTo(after) > 0);
            }

            var batch = await rows.OrderBy(r => r.Id).Take(BatchSize).ToListAsync(cancellationToken);
            foreach (var row in batch)
            {
                rebuild(row);
            }

            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
            rebuilt += batch.Count;

            if (batch.Count < BatchSize)
            {
                return rebuilt;
            }

            lastId = batch[^1].Id;
        }
    }
}
