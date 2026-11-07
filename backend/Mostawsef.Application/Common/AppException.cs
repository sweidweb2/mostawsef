using Mostawsef.Contracts.Common;

namespace Mostawsef.Application.Common;

public enum AppErrorKind
{
    /// <summary>400</summary>
    Validation,

    /// <summary>404</summary>
    NotFound,

    /// <summary>409</summary>
    Conflict,
}

/// <summary>
/// An expected failure of a use case (bad input, missing record, conflicting state). The API turns it into a
/// problem response carrying <see cref="Code"/> (and <see cref="Field"/> under "errors"); it is not a system error.
/// Codes come from <see cref="ErrorCodes"/>.
/// </summary>
public class AppException : Exception
{
    private AppException(AppErrorKind kind, string code, string? field)
        : base(field is null ? code : $"{field}: {code}")
    {
        Kind = kind;
        Code = code;
        Field = field;
    }

    public AppErrorKind Kind { get; }

    public string Code { get; }

    /// <summary>Request property the error belongs to (e.g. "Username"), or null for the whole request.</summary>
    public string? Field { get; }

    public static AppException Validation(string field, string code) => new(AppErrorKind.Validation, code, field);

    public static AppException NotFound() => new(AppErrorKind.NotFound, ErrorCodes.NotFound, null);

    public static AppException Conflict(string code, string? field = null) => new(AppErrorKind.Conflict, code, field);
}
