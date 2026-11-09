using Mostawsef.Application.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Interfaces;

public record LogCounts(int Total, int Today, int Last7Days, int Extra)
{
    public static readonly LogCounts None = new(0, 0, 0, 0);
}

/// <summary>What the deletion audit entry keeps about a removed activity log.</summary>
public record DeletedActivityLog(string Action, string? Username, DateTime OccurredAt);

/// <summary>What the deletion audit entry keeps about a removed system error.</summary>
public record DeletedSystemError(string Source, ErrorLevel Level, DateTime OccurredAt);

/// <summary>The columns the activity logs list shows; Details and UserAgent are loaded only for one record.</summary>
public record ActivityLogSummary(
    Guid Id, DateTime OccurredAt, string Action, string Module, string Description, string? Username);

/// <summary>The columns the system errors list shows; StackTrace and Details are loaded only for one record.</summary>
public record SystemErrorSummary(
    Guid Id, DateTime OccurredAt, ErrorLevel Level, string Source, string Message, string? RequestPath, int? StatusCode, string? Username);

public interface IActivityLogRepository
{
    /// <summary>Newest first. Search matches username, action, module or description.</summary>
    Task<KeysetPage<ActivityLogSummary>> GetPageAsync(PageRequest request, CancellationToken cancellationToken);

    Task<ActivityLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns null when the record does not exist.</summary>
    Task<DeletedActivityLog?> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Deletes every record that occurred before <paramref name="cutoffUtc"/>, in batches. Returns how many.</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken);

    /// <summary>Extra = failed sign-ins since <paramref name="todayStartUtc"/>.</summary>
    Task<LogCounts> GetCountsAsync(DateTime todayStartUtc, DateTime weekStartUtc, CancellationToken cancellationToken);
}

public interface ISystemErrorRepository
{
    /// <summary>Newest first. Search matches message, source, exception type, path or username.</summary>
    Task<KeysetPage<SystemErrorSummary>> GetPageAsync(PageRequest request, CancellationToken cancellationToken);

    Task<SystemError?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns null when the record does not exist.</summary>
    Task<DeletedSystemError?> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Deletes every error that occurred before <paramref name="cutoffUtc"/>, in batches. Returns how many.</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken);

    /// <summary>Extra = critical errors (all time).</summary>
    Task<LogCounts> GetCountsAsync(DateTime todayStartUtc, DateTime weekStartUtc, CancellationToken cancellationToken);
}
