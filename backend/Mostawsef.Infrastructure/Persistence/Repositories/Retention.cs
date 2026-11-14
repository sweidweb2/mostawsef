using Microsoft.EntityFrameworkCore;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

/// <summary>Batched deletion for the log retention purge.</summary>
internal static class Retention
{
    /// <summary>Rows per DELETE statement, so a large purge never holds a long lock on the table.</summary>
    public const int BatchSize = 5000;

    /// <summary>Deletes the rows of <paramref name="expired"/> oldest first, one batch per statement, until none are left.</summary>
    public static async Task<int> DeleteInBatchesAsync<T>(IOrderedQueryable<T> expired, CancellationToken cancellationToken)
    {
        var total = 0;
        int deleted;
        do
        {
            deleted = await expired.Take(BatchSize).ExecuteDeleteAsync(cancellationToken);
            total += deleted;
        }
        while (deleted == BatchSize);

        return total;
    }
}
