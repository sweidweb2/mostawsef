namespace Mostawsef.Contracts.Common;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Query string for list endpoints: ?page=1&amp;pageSize=20&amp;search=...</summary>
public record PageQuery(int Page = 1, int PageSize = 20, string? Search = null)
{
    public const int MaxPageSize = 100;

    public const int MaxSearchLength = 100;

    public int SafePage => Math.Max(1, Page);

    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);

    /// <summary>Search cut to <see cref="MaxSearchLength"/> characters.</summary>
    public string? SafeSearch => Search is { Length: > MaxSearchLength } ? Search[..MaxSearchLength] : Search;
}
