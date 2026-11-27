using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Common;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Common;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

/// <summary>
/// Keyset paging over (OccurredAt, Id): each page starts from the cursor through the (OccurredAt, Id) index,
/// so reading page 1 and page 10,000 costs the same. One extra row is read to know whether more rows follow.
/// </summary>
internal static class KeysetPaging
{
    public static async Task<KeysetPage<TItem>> ToKeysetPageAsync<TRow, TItem>(
        this IQueryable<TRow> rows,
        PageRequest request,
        Expression<Func<TRow, TItem>> select,
        Func<TItem, PageCursor> cursorOf,
        CancellationToken cancellationToken)
        where TRow : class, IChronological
    {
        // Same cap as numbered lists: a filtered total never scans past MaxCountedTotal rows.
        var total = request.IncludeTotal
            ? await rows.OrderBy(r => r.OccurredAt).ThenBy(r => r.Id)
                .Take(PageQuery.MaxCountedTotal).CountAsync(cancellationToken)
            : (int?)null;
        var items = await Window(rows, request).Select(select).ToListAsync(cancellationToken);
        var hasMore = items.Count > request.PageSize;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        if (request.Direction is PageDirection.First or PageDirection.After)
        {
            return new KeysetPage<TItem>(
                items,
                total,
                Next: hasMore ? cursorOf(items[^1]) : null,
                Previous: request.Direction == PageDirection.After && items.Count > 0 ? cursorOf(items[0]) : null);
        }

        // Fewer newer rows than a page: these are the newest, so show the first page as it starts.
        if (request.Direction == PageDirection.Before && items.Count < request.PageSize)
        {
            var first = await rows.ToKeysetPageAsync(
                request with { Direction = PageDirection.First, Cursor = null, IncludeTotal = false }, select, cursorOf, cancellationToken);
            return first with { Total = total };
        }

        items.Reverse();
        return new KeysetPage<TItem>(
            items,
            total,
            Next: request.Direction == PageDirection.Before && items.Count > 0 ? cursorOf(items[^1]) : null,
            Previous: hasMore ? cursorOf(items[0]) : null);
    }

    /// <summary>
    /// The rows of the page in read order (newest first for First/After, oldest first for Before/Last).
    /// The redundant OccurredAt bound lets the database seek the index instead of filtering from the start.
    /// </summary>
    private static IQueryable<T> Window<T>(IQueryable<T> rows, PageRequest request) where T : class, IChronological
    {
        var take = request.PageSize + 1;
        var at = request.Cursor?.OccurredAt ?? default;
        var id = request.Cursor?.Id ?? default;

        return request.Direction switch
        {
            PageDirection.After => NewestFirst(rows.Where(r => r.OccurredAt <= at && (r.OccurredAt < at || r.Id.CompareTo(id) < 0))).Take(take),
            PageDirection.Before => OldestFirst(rows.Where(r => r.OccurredAt >= at && (r.OccurredAt > at || r.Id.CompareTo(id) > 0))).Take(take),
            PageDirection.Last => OldestFirst(rows).Take(take),
            _ => NewestFirst(rows).Take(take),
        };
    }

    private static IQueryable<T> NewestFirst<T>(IQueryable<T> rows) where T : class, IChronological =>
        rows.OrderByDescending(r => r.OccurredAt).ThenByDescending(r => r.Id);

    private static IQueryable<T> OldestFirst<T>(IQueryable<T> rows) where T : class, IChronological =>
        rows.OrderBy(r => r.OccurredAt).ThenBy(r => r.Id);
}
