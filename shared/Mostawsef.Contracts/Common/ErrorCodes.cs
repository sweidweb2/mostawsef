namespace Mostawsef.Contracts.Common;

/// <summary>
/// Stable error codes returned by the API in problem responses: the top-level "code" and the values of "errors".
/// A code may carry arguments after '|' (e.g. "max_length|50"). The desktop app translates "ErrorCode.&lt;code&gt;".
/// Use them as ErrorMessage of validation attributes, so the API and the desktop app report the same codes.
/// </summary>
public static class ErrorCodes
{
    public const char ArgumentSeparator = '|';

    // Validation attributes. {1} is the attribute's length argument (Range: minimum, {2} maximum).
    public const string Required = "required";
    public const string MaxLength = "max_length|{1}";
    public const string MinLength = "min_length|{1}";
    public const string Range = "range|{1}|{2}";
    public const string Invalid = "invalid";

    // Problem codes
    public const string Validation = "validation";
    public const string NotFound = "not_found";
    public const string ConcurrencyConflict = "concurrency_conflict";

    /// <summary>Another record already has this value (e.g. a lab test code); set on that field.</summary>
    public const string Taken = "taken";

    // Users and passwords
    public const string UsernameTaken = "username_taken";
    public const string OwnAccount = "own_account";
    public const string WrongPassword = "wrong_password";

    /// <summary>Desktop only: a "confirm password" field that does not match.</summary>
    public const string PasswordMismatch = "password_mismatch";
}
