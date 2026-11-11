using Microsoft.EntityFrameworkCore;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

/// <summary>
/// Builds a LIKE '%term%' pattern for a lowercased SearchText column so the GIN trigram index can serve it.
/// The term's LIKE wildcards are escaped, so "%" and "_" match themselves.
/// </summary>
internal static class TrigramSearch
{
    public const string EscapeCharacter = "\\";

    public static string? ContainsPattern(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var normalized = SearchText.Normalize(search);
        if (normalized.Length == 0)
        {
            return null;
        }

        var escaped = EscapeLike(normalized);
        return string.Create(escaped.Length + 2, escaped, static (span, term) =>
        {
            span[0] = '%';
            term.AsSpan().CopyTo(span[1..]);
            span[^1] = '%';
        });
    }

    private static string EscapeLike(string term)
    {
        var extra = 0;
        foreach (var c in term)
        {
            if (c is '\\' or '%' or '_')
            {
                extra++;
            }
        }

        if (extra == 0)
        {
            return term;
        }

        return string.Create(term.Length + extra, term, static (span, value) =>
        {
            var i = 0;
            foreach (var c in value)
            {
                if (c is '\\' or '%' or '_')
                {
                    span[i++] = '\\';
                }

                span[i++] = c;
            }
        });
    }
}

/// <summary>List search over activity and system-error SearchText columns.</summary>
public static class LogSearch
{
    /// <summary>Sets SearchText from the searchable fields (new rows and the backfill of older ones).</summary>
    public static void FillSearchText(this ActivityLog log) =>
        log.SetSearchText(SearchText.Key(log.Username, log.Action, log.Module, log.Description));

    public static void FillSearchText(this SystemError error) =>
        error.SetSearchText(SearchText.Key(error.Message, error.Source, error.ExceptionType, error.RequestPath, error.Username));

    public static IQueryable<ActivityLog> Matching(this IQueryable<ActivityLog> logs, string? search) =>
        TrigramSearch.ContainsPattern(search) is { } pattern
            ? logs.Where(l => EF.Functions.Like(l.SearchText, pattern, TrigramSearch.EscapeCharacter))
            : logs;

    public static IQueryable<SystemError> Matching(this IQueryable<SystemError> errors, string? search) =>
        TrigramSearch.ContainsPattern(search) is { } pattern
            ? errors.Where(e => EF.Functions.Like(e.SearchText, pattern, TrigramSearch.EscapeCharacter))
            : errors;
}

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