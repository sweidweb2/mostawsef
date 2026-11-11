using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Application.Common;

/// <summary>Position of a row in a newest-first list. Clients only see the encoded form and send it back unchanged.</summary>
public readonly record struct PageCursor(DateTime OccurredAt, Guid Id)
{
    public string Encode() => string.Create(CultureInfo.InvariantCulture, $"{OccurredAt.Ticks}-{Id:N}");

    public static bool TryParse(string? value, out PageCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var separator = value.IndexOf('-');
        if (separator <= 0
            || !long.TryParse(value.AsSpan(0, separator), NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || ticks > DateTime.MaxValue.Ticks
            || !Guid.TryParseExact(value.AsSpan(separator + 1), "N", out var id))
        {
            return false;
        }

        cursor = new PageCursor(new DateTime(ticks, DateTimeKind.Utc), id);
        return true;
    }
}

public enum PageDirection
{
    /// <summary>The newest rows.</summary>
    First,

    /// <summary>The rows just older than the cursor.</summary>
    After,

    /// <summary>The rows just newer than the cursor.</summary>
    Before,

    /// <summary>The oldest rows.</summary>
    Last,
}

/// <summary>A validated <see cref="CursorQuery"/>.</summary>
public sealed record PageRequest(PageDirection Direction, PageCursor? Cursor, int PageSize, string? Search, bool IncludeTotal)
{
    public const string InvalidMessage = "Use at most one of after, before and last, with a cursor returned by this list.";

    public static bool TryCreate(CursorQuery query, [NotNullWhen(true)] out PageRequest? request)
    {
        request = null;
        if ((query.After is null ? 0 : 1) + (query.Before is null ? 0 : 1) + (query.Last ? 1 : 0) > 1)
        {
            return false;
        }

        PageCursor? cursor = null;
        if ((query.After ?? query.Before) is { } text)
        {
            if (!PageCursor.TryParse(text, out var parsed))
            {
                return false;
            }

            cursor = parsed;
        }

        var direction = query.After is not null ? PageDirection.After
            : query.Before is not null ? PageDirection.Before
            : query.Last ? PageDirection.Last
            : PageDirection.First;

        request = new PageRequest(direction, cursor, query.SafePageSize, query.Search, query.IncludeTotal);
        return true;
    }

    /// <summary>Throws AppException (400, "min_length|3" on Search) when the search term is too short for the search index.</summary>
    public void EnsureSearchable()
    {
        if (CursorQuery.IsSearchTooShort(Search))
        {
            throw AppException.Validation(
                nameof(CursorQuery.Search),
                string.Format(CultureInfo.InvariantCulture, ErrorCodes.MinLength, nameof(CursorQuery.Search), CursorQuery.MinSearchLength));
        }
    }
}

/// <summary>Shared search-length check for offset-paged lists that use a trigram SearchText index.</summary>
public static class PageQuerySearch
{
    /// <summary>Throws AppException (400, "min_length|3" on Search) when the search term is too short for the search index.</summary>
    public static void EnsureSearchable(this PageQuery query)
    {
        if (PageQuery.IsSearchTooShort(query.Search))
        {
            throw AppException.Validation(
                nameof(PageQuery.Search),
                string.Format(CultureInfo.InvariantCulture, ErrorCodes.MinLength, nameof(PageQuery.Search), PageQuery.MinSearchLength));
        }
    }
}

/// <summary>Rows of one page, newest first, with the cursors to the neighbouring pages (null at either end).</summary>
public sealed record KeysetPage<T>(IReadOnlyList<T> Items, int? Total, PageCursor? Next, PageCursor? Previous)
{
    public CursorPage<TDto> ToResult<TDto>(PageRequest request, Func<T, TDto> map) =>
        new(Items.Select(map).ToList(), request.PageSize, Total, Next?.Encode(), Previous?.Encode());
}
