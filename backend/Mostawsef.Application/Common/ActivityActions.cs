namespace Mostawsef.Application.Common;

/// <summary>Module names shown next to each activity.</summary>
public static class ActivityModules
{
    public const string Auth = "Auth";
    public const string Logs = "Logs";
    public const string Users = "Users";
}

/// <summary>
/// Stable activity codes ("module.verb"). Add one per main action; the desktop app shows a translated
/// label for each code (resource key "Action.&lt;code&gt;").
/// </summary>
public static class ActivityActions
{
    public const string SignedIn = "auth.signed_in";
    public const string SignInFailed = "auth.sign_in_failed";
    public const string SignedOut = "auth.signed_out";
    public const string PasswordChanged = "auth.password_changed";

    public const string ActivityLogDeleted = "logs.activity_deleted";
    public const string SystemErrorDeleted = "logs.error_deleted";
    public const string LogsPurged = "logs.purged";

    public const string UserCreated = "users.created";
    public const string UserUpdated = "users.updated";
    public const string UserDeleted = "users.deleted";
    public const string UserPasswordReset = "users.password_reset";
}
