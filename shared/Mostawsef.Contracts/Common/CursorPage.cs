namespace Mostawsef.Contracts.Common;

/// <summary>
/// One page of a large newest-first list (activity logs, system errors). Move with the opaque cursors:
/// NextCursor as After (older rows), PreviousCursor as Before (newer rows); null means nothing further that way.
/// TotalCount is null when the request set IncludeTotal=false.
/// </summary>
public record CursorPage<T>(IReadOnlyList<T> Items, int PageSize, int? TotalCount, string? NextCursor, string? PreviousCursor);

/// <summary>
/// Query string for keyset-paged lists: ?pageSize=20&amp;search=...&amp;after=|before=|last=true&amp;includeTotal=false.
/// Without after, before or last it returns the first (newest) page. Each page costs the same however deep it is;
/// counting the total is the expensive part, so ask for it only when the filter changes.
/// </summary>
public record CursorQuery(
    int PageSize = 20,
    string? Search = null,
    string? After = null,
    string? Before = null,
    bool Last = false,
    bool IncludeTotal = true)
{
    public const int MaxPageSize = PageQuery.MaxPageSize;

    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}
