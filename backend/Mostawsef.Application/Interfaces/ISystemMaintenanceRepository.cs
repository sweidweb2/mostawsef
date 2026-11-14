namespace Mostawsef.Application.Interfaces;

/// <summary>Whole-system totals for the Super dashboard. Money is in minor units.</summary>
/// <param name="Revenue">Every payment minus every refund.</param>
/// <param name="Outstanding">What pending and partly paid orders still owe.</param>
public record DashboardTotals(
    int Users, int ActiveUsers, int Patients, int LabTests, int Orders, long Revenue, long Outstanding);

/// <summary>A value summed over one UTC hour (the start of that hour).</summary>
public record HourlyValue(DateTime HourUtc, long Value);

/// <summary>Per-hour series since a moment; hours without rows are absent. Grouped per hour so the caller can map hours to local days.</summary>
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

    /// <summary>Orders, new patients, net payments, activities and errors per UTC hour since <paramref name="sinceUtc"/>.</summary>
    Task<DashboardHourlySeries> GetHourlySeriesAsync(DateTime sinceUtc, CancellationToken cancellationToken);

    /// <summary>
    /// In one transaction, empties every business and log table (payments, order items, orders, patients, lab tests,
    /// activity logs, system errors, number sequences). Users, clinic/system settings and backup records stay.
    /// </summary>
    Task<DataResetCounts> ResetDataAsync(CancellationToken cancellationToken);
}
