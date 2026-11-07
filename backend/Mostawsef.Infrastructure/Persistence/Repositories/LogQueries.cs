using Microsoft.EntityFrameworkCore;
using Mostawsef.Domain.Common;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

/// <summary>
/// List search over the lowercased SearchText column: one LIKE '%term%' that the column's trigram index can serve.
/// The term's LIKE wildcards are escaped, so "%" and "_" match themselves.
/// </summary>
public static class LogSearch
{
    private const string EscapeCharacter = "\\";

    public static IQueryable<ActivityLog> Matching(this IQueryable<ActivityLog> logs, string? search) =>
        ContainsPattern(search) is { } pattern
            ? logs.Where(l => EF.Functions.Like(l.SearchText, pattern, EscapeCharacter))
            : logs;

    public static IQueryable<SystemError> Matching(this IQueryable<SystemError> errors, string? search) =>
        ContainsPattern(search) is { } pattern
            ? errors.Where(e => EF.Functions.Like(e.SearchText, pattern, EscapeCharacter))
            : errors;

    private static string? ContainsPattern(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var term = Text.NormalizeSearchTerm(search)
            .Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter)
            .Replace("%", EscapeCharacter + "%")
            .Replace("_", EscapeCharacter + "_");
        return $"%{term}%";
    }
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