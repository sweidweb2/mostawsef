using Mostawsef.Domain.Common;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Domain.Entities;

/// <summary>An unexpected failure in the API or the desktop app. Append-only.</summary>
public class SystemError
{
    public const int SourceMaxLength = 50;
    public const int MessageMaxLength = 2000;
    public const int ExceptionTypeMaxLength = 300;
    public const int StackTraceMaxLength = 20000;
    public const int RequestPathMaxLength = 500;
    public const int RequestMethodMaxLength = 10;
    public const int UsernameMaxLength = 50;
    public const int TraceIdMaxLength = 100;
    public const int IpAddressMaxLength = 45;
    public const int DetailsMaxLength = 8000;

    private SystemError()
    {
    }

    public SystemError(
        ErrorLevel level,
        string source,
        string message,
        string? exceptionType,
        string? stackTrace,
        string? requestPath,
        string? requestMethod,
        int? statusCode,
        Guid? userId,
        string? username,
        string? traceId,
        string? ipAddress,
        string? details)
    {
        Level = level;
        Source = Text.TruncateRequired(source, SourceMaxLength);
        Message = Text.TruncateRequired(message, MessageMaxLength);
        ExceptionType = Text.Truncate(exceptionType, ExceptionTypeMaxLength);
        StackTrace = Text.Truncate(stackTrace, StackTraceMaxLength);
        RequestPath = Text.Truncate(requestPath, RequestPathMaxLength);
        RequestMethod = Text.Truncate(requestMethod, RequestMethodMaxLength);
        StatusCode = statusCode;
        UserId = userId;
        Username = Text.Truncate(username, UsernameMaxLength);
        TraceId = Text.Truncate(traceId, TraceIdMaxLength);
        IpAddress = Text.Truncate(ipAddress, IpAddressMaxLength);
        Details = Text.Truncate(details, DetailsMaxLength);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public DateTime OccurredAt { get; private set; } = DateTime.UtcNow;

    public ErrorLevel Level { get; private set; }

    /// <summary>Where it happened: "Api", "Desktop".</summary>
    public string Source { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public string? ExceptionType { get; private set; }

    public string? StackTrace { get; private set; }

    public string? RequestPath { get; private set; }

    public string? RequestMethod { get; private set; }

    public int? StatusCode { get; private set; }

    public Guid? UserId { get; private set; }

    public string? Username { get; private set; }

    public string? TraceId { get; private set; }

    public string? IpAddress { get; private set; }

    /// <summary>Extra context as JSON (app version, OS, screen...).</summary>
    public string? Details { get; private set; }
}
