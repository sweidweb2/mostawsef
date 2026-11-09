namespace Mostawsef.Contracts.Common;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// Query string for list endpoints: ?page=1&amp;pageSize=20&amp;search=...&amp;includeTotal=false.
/// Counting the filtered total is the expensive part on large tables, so ask for it only when the filter changes
/// (first load, search, refresh). Page flips can set IncludeTotal=false; the total then comes back as -1.
/// </summary>
public record PageQuery(int Page = 1, int PageSize = 20, string? Search = null, bool IncludeTotal = true)
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    public const int MaxSearchLength = 100;

    /// <summary>Same floor as <see cref="CursorQuery.MinSearchLength"/>: trigram search needs three characters.</summary>
    public const int MinSearchLength = CursorQuery.MinSearchLength;

    /// <summary>Sentinel when <see cref="IncludeTotal"/> is false: the client keeps its previous total.</summary>
    public const int TotalNotComputed = -1;

    public int SafePage => Math.Max(1, Page);

    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);

    /// <summary>Search cut to <see cref="MaxSearchLength"/> characters.</summary>
    public string? SafeSearch => Search is { Length: > MaxSearchLength } ? Search[..MaxSearchLength] : Search;

    /// <summary>True for a term with fewer than <see cref="MinSearchLength"/> letters or digits. Blank means "no search".</summary>
    public static bool IsSearchTooShort(string? search) => CursorQuery.IsSearchTooShort(search);
}
