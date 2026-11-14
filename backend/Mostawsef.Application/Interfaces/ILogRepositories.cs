using Mostawsef.Application.Common;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Interfaces;

public record LogCounts(int Total, int Today, int Last7Days, int Extra)
{
    public static readonly LogCounts None = new(0, 0, 0, 0);
}

/// <summary>The columns the activity logs list shows; Details and UserAgent are loaded only for one record.</summary>
public record ActivityLogSummary(
    Guid Id, DateTime OccurredAt, string Action, string Module, string Description, string? Username);

/// <summary>Activity log details without SearchText.</summary>
public record ActivityLogDetails(
    Guid Id,
    DateTime OccurredAt,
    string Action,
    string Module,
    string Description,
    Guid? UserId,
    string? Username,
    string? EntityType,
    string? EntityId,
    string? IpAddress,
    string? UserAgent,
    string? Details);

/// <summary>The columns the system errors list shows; StackTrace and Details are loaded only for one record.</summary>
public record SystemErrorSummary(
    Guid Id, DateTime OccurredAt, ErrorLevel Level, string Source, string Message, string? RequestPath, int? StatusCode, string? Username);

/// <summary>System error details without SearchText.</summary>
public record SystemErrorDetails(
    Guid Id,
    DateTime OccurredAt,
    ErrorLevel Level,
    string Source,
    string Message,
    string? ExceptionType,
    string? StackTrace,
    string? RequestPath,
    string? RequestMethod,
    int? StatusCode,
    Guid? UserId,
    string? Username,
    string? TraceId,
    string? IpAddress,
    string? Details);

public interface IActivityLogRepository
{
    /// <summary>Newest first. Search matches username, action, module or description.</summary>
    Task<KeysetPage<ActivityLogSummary>> GetPageAsync(PageRequest request, CancellationToken cancellationToken);

    Task<ActivityLogDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Deletes the records with these ids (missing ones are skipped). Returns how many.</summary>
    Task<int> DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Deletes every record, in batches. Returns how many.</summary>
    Task<int> DeleteAllAsync(CancellationToken cancellationToken);

    /// <summary>Deletes every record that occurred before <paramref name="cutoffUtc"/>, in batches. Returns how many.</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken);

    /// <summary>Extra = failed sign-ins since <paramref name="todayStartUtc"/>.</summary>
    Task<LogCounts> GetCountsAsync(DateTime todayStartUtc, DateTime weekStartUtc, CancellationToken cancellationToken);
}

public interface ISystemErrorRepository
{
    /// <summary>Newest first. Search matches message, source, exception type, path or username.</summary>
    Task<KeysetPage<SystemErrorSummary>> GetPageAsync(PageRequest request, CancellationToken cancellationToken);

    Task<SystemErrorDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>True when a row was deleted.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Deletes the errors with these ids (missing ones are skipped). Returns how many.</summary>
    Task<int> DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Deletes every error, in batches. Returns how many.</summary>
    Task<int> DeleteAllAsync(CancellationToken cancellationToken);

    /// <summary>Deletes every error that occurred before <paramref name="cutoffUtc"/>, in batches. Returns how many.</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken);

    /// <summary>Extra = critical errors (all time).</summary>
    Task<LogCounts> GetCountsAsync(DateTime todayStartUtc, DateTime weekStartUtc, CancellationToken cancellationToken);
}
