using System.ComponentModel.DataAnnotations;
using Mostawsef.Contracts.Common;

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

/// <summary>One calendar day (server local time) of the Super dashboard. Revenue is in minor units: payments minus refunds.</summary>
public record DashboardDayDto(DateOnly Date, int Orders, int Patients, long Revenue, int Activity, int Errors);

/// <summary>
/// Whole-system figures for the Super dashboard. Money values are minor units of <see cref="Currency"/>:
/// Revenue = all payments minus refunds, Outstanding = what pending and partly paid orders still owe.
/// </summary>
public record SuperDashboardDto(
    string Currency,
    int Users,
    int ActiveUsers,
    int Patients,
    int LabTests,
    int Orders,
    long Revenue,
    long Outstanding,
    int ActivityToday,
    int ErrorsToday,
    IReadOnlyList<StatusCountDto> OrderStatuses,
    IReadOnlyList<DashboardDayDto> Days);
