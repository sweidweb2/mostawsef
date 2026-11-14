using Microsoft.EntityFrameworkCore;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

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
