using Microsoft.EntityFrameworkCore;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

/// <summary>
/// Numbered list paging: when the total is needed, each page row carries <c>COUNT(*)</c> as a scalar subquery
/// so the page and the filtered total arrive in one round-trip. Page flips pass <see cref="PageQuery.IncludeTotal"/> false.
/// </summary>
internal static class PageQueryExecutor
{
    public static async Task<(IReadOnlyList<T> Items, int Total)> ToPageAsync<T>(
        this IQueryable<T> rows,
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

        var page = await pageRows
            .Select(row => new { Row = row, Total = rows.Count() })
            .ToListAsync(cancellationToken);

        if (page.Count == 0)
        {
            // Empty page (no matches, or past the last page): still need the total for the client.
            return ([], await rows.CountAsync(cancellationToken));
        }

        var items = new List<T>(page.Count);
        for (var i = 0; i < page.Count; i++)
        {
            items.Add(page[i].Row);
        }

        return (items, page[0].Total);
    }
}
