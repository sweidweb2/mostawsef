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

    /// <summary>The record is referenced elsewhere (e.g. a lab test used by orders) and cannot be deleted.</summary>
    public const string InUse = "in_use";

    /// <summary>The patient has orders, so deleting them would leave those orders (and any balance owed) without a patient.</summary>
    public const string PatientHasOrders = "patient_has_orders";

    // Orders

    /// <summary>A lab test that is no longer offered; set on that item's field.</summary>
    public const string Inactive = "inactive";

    /// <summary>The same value appears twice in a list (e.g. a test ordered twice); set on the repeated item.</summary>
    public const string Duplicate = "duplicate";

    /// <summary>A payment larger than the order total.</summary>
    public const string ExceedsTotal = "exceeds_total";

    /// <summary>A payment larger than what the order still owes.</summary>
    public const string ExceedsBalance = "exceeds_balance";

    // Users and passwords
    public const string UsernameTaken = "username_taken";
    public const string OwnAccount = "own_account";
    public const string WrongPassword = "wrong_password";

    /// <summary>Sign-in refused because maintenance mode is on (only Super can sign in).</summary>
    public const string MaintenanceMode = "maintenance_mode";

    /// <summary>Desktop only: a "confirm password" field that does not match.</summary>
    public const string PasswordMismatch = "password_mismatch";
}
