using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Interfaces;

/// <summary>One error to record. Request and user info are filled from the current request when not set.</summary>
public record SystemErrorEntry(ErrorLevel Level, string Source, string Message)
{
    public string? ExceptionType { get; init; }

    public string? StackTrace { get; init; }

    public int? StatusCode { get; init; }

    public Guid? UserId { get; init; }

    public string? Username { get; init; }

    public object? Details { get; init; }
}

/// <summary>
/// System error engine. Unhandled API exceptions are recorded automatically by the exception middleware;
/// call it directly only for errors you catch and handle yourself:
/// <code>catch (Exception ex) { await errors.LogAsync(ex, details: new { patientId }); }</code>
/// Never throws.
/// </summary>
public interface ISystemErrorLogger
{
    Task LogAsync(
        Exception exception,
        ErrorLevel level = ErrorLevel.Error,
        int? statusCode = null,
        object? details = null,
        CancellationToken cancellationToken = default);

    Task LogAsync(SystemErrorEntry entry, CancellationToken cancellationToken = default);
}
