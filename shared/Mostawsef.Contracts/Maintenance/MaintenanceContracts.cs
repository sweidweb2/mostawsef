using System.ComponentModel.DataAnnotations;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Dashboards;

namespace Mostawsef.Contracts.Maintenance;

public static class DataResetRules
{
    /// <summary>The word the Super user types to confirm a data reset (checked by the desktop and the API, case-sensitive).</summary>
    public const string ConfirmationWord = "RESET";
}

/// <summary>Deletes every business record and log; user accounts and the clinic/system settings stay.</summary>
public record ResetDataRequest
{
    /// <summary>Must equal <see cref="DataResetRules.ConfirmationWord"/>, otherwise 400 with "invalid" on this field.</summary>
    [Required(ErrorMessage = ErrorCodes.Required)]
    public string? Confirmation { get; init; }
}

/// <summary>How many rows of each kind the reset removed.</summary>
public record ResetDataResultDto(int Patients, int Orders, int Payments, int LabTests, int ActivityLogs, int SystemErrors);

/// <summary>Orders in one status (every status is listed, zero included).</summary>
public record StatusCountDto(string Status, int Count);

/// <summary>
/// One graph point of the Super dashboard: the start of its hour, day or month (server local time) and what happened
/// in it. Revenue is in minor units: payments minus refunds.
/// </summary>
public record DashboardPointDto(DateTime Start, int Orders, int Patients, long Revenue, int Activity, int Errors);

/// <summary>
/// The Super dashboard for one period (GET api/system/dashboard). Money values are minor units of <see cref="Currency"/>.
/// For the period: Orders and OrderStatuses count orders created in it, Patients the new patients, Revenue the payments
/// minus refunds made in it, Outstanding what its pending and partly paid orders still owe, Activity and Errors the
/// log entries. Users, ActiveUsers and LabTests are current totals. Points list every bucket, zero included.
/// </summary>
public record SuperDashboardDto(
    string Currency,
    DashboardRangeDto Range,
    int Users,
    int ActiveUsers,
    int LabTests,
    int Orders,
    int Patients,
    long Revenue,
    long Outstanding,
    int Activity,
    int Errors,
    IReadOnlyList<StatusCountDto> OrderStatuses,
    IReadOnlyList<DashboardPointDto> Points);
