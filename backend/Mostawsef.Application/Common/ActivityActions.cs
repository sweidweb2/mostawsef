namespace Mostawsef.Application.Common;

/// <summary>Module names shown next to each activity.</summary>
public static class ActivityModules
{
    public const string Auth = "Auth";
    public const string Logs = "Logs";
    public const string Users = "Users";
    public const string Settings = "Settings";
    public const string LabTests = "LabTests";
    public const string Patients = "Patients";
    public const string Orders = "Orders";
    public const string Backups = "Backups";
    public const string System = "System";
}

/// <summary>
/// Stable activity codes ("module.verb"). Add one per main action; the desktop app shows a translated
/// label for each code (resource key "Action.&lt;code&gt;").
/// </summary>
public static class ActivityActions
{
    public const string SignedIn = "auth.signed_in";
    public const string SignInFailed = "auth.sign_in_failed";
    public const string SignInBlocked = "auth.sign_in_blocked";
    public const string SignedOut = "auth.signed_out";
    public const string PasswordChanged = "auth.password_changed";

    public const string SystemErrorDeleted = "logs.error_deleted";
    public const string LogsPurged = "logs.purged";
    public const string ActivityLogsDeleted = "logs.activity_deleted";
    public const string ActivityLogsCleared = "logs.activity_cleared";
    public const string SystemErrorsDeleted = "logs.errors_deleted";
    public const string SystemErrorsCleared = "logs.errors_cleared";

    public const string UserCreated = "users.created";
    public const string UserUpdated = "users.updated";
    public const string UserDeleted = "users.deleted";
    public const string UserPasswordReset = "users.password_reset";

    public const string ClinicSettingsUpdated = "settings.clinic_updated";
    public const string SystemSettingsUpdated = "settings.system_updated";

    public const string LabTestCreated = "lab_tests.created";
    public const string LabTestUpdated = "lab_tests.updated";
    public const string LabTestDeleted = "lab_tests.deleted";

    public const string PatientCreated = "patients.created";
    public const string PatientUpdated = "patients.updated";
    public const string PatientDeleted = "patients.deleted";
    public const string PatientDeleteRefused = "patients.delete_refused";

    public const string OrderCreated = "orders.created";
    public const string OrderPaymentAdded = "orders.payment_added";

    public const string BackupCreated = "backups.created";
    public const string BackupExported = "backups.exported";

    public const string DataReset = "system.data_reset";
}
