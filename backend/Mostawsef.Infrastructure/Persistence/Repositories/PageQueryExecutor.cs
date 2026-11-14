using Microsoft.EntityFrameworkCore;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

/// <summary>
/// Numbered list paging: when the total is needed, each page row carries the count as a scalar subquery
/// so the page and the filtered total arrive in one round-trip. Page flips pass <see cref="PageQuery.IncludeTotal"/> false.
/// The count stops at <see cref="PageQuery.MaxCountedTotal"/> rows, so it costs the same on a table of any size.
/// </summary>
internal static class PageQueryExecutor
{
    /// <param name="rows">The sorted, projected page rows.</param>
    /// <param name="filtered">The same rows, filtered but not sorted: counted without an ORDER BY.</param>
    public static async Task<(IReadOnlyList<T> Items, int Total)> ToPageAsync<TRow, T>(
        this IQueryable<T> rows,
        IQueryable<TRow> filtered,
        PageQuery query,
        CancellationToken cancellationToken)
    {
        var pageRows = rows
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize);

        if (!query.IncludeTotal)
        {
            return (await pageRows.ToListAsync(cancellationToken), PageQuery.TotalNotComputed);
        }

        var counted = filtered.Take(PageQuery.MaxCountedTotal);
        var page = await pageRows
            .Select(row => new { Row = row, Total = counted.Count() })
            .ToListAsync(cancellationToken);

        if (page.Count == 0)
        {
            // Empty page (no matches, or past the last page): still need the total for the client.
            return ([], await counted.CountAsync(cancellationToken));
        }

        var items = new List<T>(page.Count);
        for (var i = 0; i < page.Count; i++)
        {
            items.Add(page[i].Row);
        }

        return (items, page[0].Total);
    }
}
