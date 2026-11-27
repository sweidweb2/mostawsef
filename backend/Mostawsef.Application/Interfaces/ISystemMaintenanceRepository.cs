namespace Mostawsef.Application.Interfaces;

/// <summary>Current totals for the Super dashboard (not tied to its period).</summary>
public record DashboardTotals(int Users, int ActiveUsers, int LabTests);

/// <summary>A value summed over one UTC hour (the start of that hour).</summary>
public record HourlyValue(DateTime HourUtc, long Value);

/// <summary>Per-hour series of a UTC window; hours without rows are absent. Grouped per hour so the caller can map hours to local buckets.</summary>
public record DashboardHourlySeries(
    IReadOnlyList<HourlyValue> Orders,
    IReadOnlyList<HourlyValue> Patients,
    IReadOnlyList<HourlyValue> Revenue,
    IReadOnlyList<HourlyValue> Activity,
    IReadOnlyList<HourlyValue> Errors);

/// <summary>Rows removed by a data reset.</summary>
public record DataResetCounts(int Patients, int Orders, int Payments, int LabTests, int ActivityLogs, int SystemErrors);

public interface ISystemMaintenanceRepository
{
    Task<DashboardTotals> GetTotalsAsync(CancellationToken cancellationToken);

    /// <summary>What the pending and partly paid orders created in [<paramref name="sinceUtc"/>, <paramref name="untilUtc"/>) still owe (minor units).</summary>
    Task<long> GetOutstandingAsync(DateTime sinceUtc, DateTime untilUtc, CancellationToken cancellationToken);

    /// <summary>Orders, new patients, net payments, activities and errors per UTC hour in [<paramref name="sinceUtc"/>, <paramref name="untilUtc"/>).</summary>
    Task<DashboardHourlySeries> GetHourlySeriesAsync(DateTime sinceUtc, DateTime untilUtc, CancellationToken cancellationToken);

    /// <summary>The oldest moment (UTC) of any order, patient, payment, activity or error; null when there is none.</summary>
    Task<DateTime?> GetFirstRecordAtAsync(CancellationToken cancellationToken);

    /// <summary>
    /// In one transaction, empties every business and log table (payments, order items, orders, patients, lab tests,
    /// activity logs, system errors, number sequences, backup records, scheduled dumps). Users and clinic/system
    /// settings stay.
    /// </summary>
    Task<DataResetCounts> ResetDataAsync(CancellationToken cancellationToken);
}
