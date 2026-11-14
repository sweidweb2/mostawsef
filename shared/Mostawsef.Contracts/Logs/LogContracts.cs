using System.ComponentModel.DataAnnotations;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Contracts.Logs;

// Activity logs

public record ActivityLogListItemDto(
    Guid Id,
    DateTime OccurredAt,
    string Action,
    string Module,
    string Description,
    string? Username);

public record ActivityLogDto(
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

public record ActivityLogStatsDto(int Total, int Today, int Last7Days, int FailedSignInsToday);

// System errors

public record SystemErrorListItemDto(
    Guid Id,
    DateTime OccurredAt,
    string Level,
    string Source,
    string Message,
    string? RequestPath,
    int? StatusCode,
    string? Username);

public record SystemErrorDto(
    Guid Id,
    DateTime OccurredAt,
    string Level,
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

public record SystemErrorStatsDto(int Total, int Today, int Last7Days, int Critical);

/// <summary>Sent by the desktop app when it catches an unexpected exception. Message may not be empty or whitespace.</summary>
public record ReportClientErrorRequest(
    [Required] string Message,
    string? ExceptionType,
    string? StackTrace,
    string? Level,
    string? Context,
    string? AppVersion,
    string? OsVersion);

public static class ErrorLevels
{
    public const string Warning = "Warning";
    public const string Error = "Error";
    public const string Critical = "Critical";
}

public static class LogRules
{
    /// <summary>Most records one bulk delete request may name.</summary>
    public const int MaxBulkDelete = 1000;
}

/// <summary>Deletes the named activity logs or system errors; ids that no longer exist are skipped.</summary>
public record DeleteLogsRequest
{
    [Required(ErrorMessage = ErrorCodes.Required)]
    [MinLength(1, ErrorMessage = ErrorCodes.Required)]
    [MaxLength(LogRules.MaxBulkDelete, ErrorMessage = ErrorCodes.MaxLength)]
    public IReadOnlyList<Guid> Ids { get; init; } = [];
}

/// <summary>How many records a bulk delete or "delete all" removed.</summary>
public record DeleteLogsResultDto(int Deleted);
