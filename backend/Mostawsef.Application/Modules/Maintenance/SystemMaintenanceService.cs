using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Application.Modules.Settings;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Dashboards;
using Mostawsef.Contracts.Maintenance;
using Mostawsef.Contracts.Orders;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Modules.Maintenance;

public interface ISystemMaintenanceService
{
    /// <summary>
    /// The Super dashboard for a period (see <see cref="DashboardRangeQuery"/>), kept between the first recorded day
    /// and today: period totals, orders per status and per-bucket series, plus current user and lab test totals.
    /// </summary>
    Task<SuperDashboardDto> GetDashboardAsync(DashboardRangeQuery query, CancellationToken cancellationToken);

    /// <summary>Empties every business and log table once the confirmation word matches; accounts and settings stay. Logged as an activity.</summary>
    Task<ResetDataResultDto> ResetDataAsync(ResetDataRequest request, CancellationToken cancellationToken);
}

public class SystemMaintenanceService(
    ISystemMaintenanceRepository repository,
    IOrderRepository orders,
    IClinicSettingsService clinic,
    IActivityLogger activity,
    TimeProvider time) : ISystemMaintenanceService
{
    public async Task<SuperDashboardDto> GetDashboardAsync(DashboardRangeQuery query, CancellationToken cancellationToken)
    {
        // One DbContext per request: the queries run one after another (each is a single indexed statement).
        var range = DashboardRange.Resolve(query, await repository.GetFirstRecordAtAsync(cancellationToken), time);
        var currency = await clinic.GetCurrencyCodeAsync(cancellationToken);
        var totals = await repository.GetTotalsAsync(cancellationToken);
        var outstanding = await repository.GetOutstandingAsync(range.SinceUtc, range.UntilUtc, cancellationToken);
        var statusCounts = await orders.GetStatusCountsAsync(range.SinceUtc, range.UntilUtc, cancellationToken);
        var series = await repository.GetHourlySeriesAsync(range.SinceUtc, range.UntilUtc, cancellationToken);

        var orderBuckets = range.Sum(series.Orders);
        var patientBuckets = range.Sum(series.Patients);
        var revenueBuckets = range.Sum(series.Revenue);
        var activityBuckets = range.Sum(series.Activity);
        var errorBuckets = range.Sum(series.Errors);

        var points = new List<DashboardPointDto>();
        int orderTotal = 0, patientTotal = 0, activityTotal = 0, errorTotal = 0;
        long revenueTotal = 0;
        foreach (var start in range.BucketStarts())
        {
            var point = new DashboardPointDto(
                start,
                (int)orderBuckets.GetValueOrDefault(start),
                (int)patientBuckets.GetValueOrDefault(start),
                revenueBuckets.GetValueOrDefault(start),
                (int)activityBuckets.GetValueOrDefault(start),
                (int)errorBuckets.GetValueOrDefault(start));
            points.Add(point);
            orderTotal += point.Orders;
            patientTotal += point.Patients;
            revenueTotal += point.Revenue;
            activityTotal += point.Activity;
            errorTotal += point.Errors;
        }

        var statuses = OrderStatuses.All
            .Select(s => new StatusCountDto(s, statusCounts.GetValueOrDefault(Enum.Parse<OrderStatus>(s))))
            .ToList();

        return new SuperDashboardDto(
            currency,
            range.ToDto(),
            totals.Users,
            totals.ActiveUsers,
            totals.LabTests,
            orderTotal,
            patientTotal,
            revenueTotal,
            outstanding,
            activityTotal,
            errorTotal,
            statuses,
            points);
    }

    public async Task<ResetDataResultDto> ResetDataAsync(ResetDataRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Confirmation?.Trim(), DataResetRules.ConfirmationWord, StringComparison.Ordinal))
        {
            throw AppException.Validation(nameof(ResetDataRequest.Confirmation), ErrorCodes.Invalid);
        }

        var counts = await repository.ResetDataAsync(cancellationToken);

        // Written after the reset, so it is the first record of the emptied activity log.
        await activity.LogAsync(
            new ActivityEntry(ActivityActions.DataReset, ActivityModules.System, "Reset the database: deleted all data except user accounts and settings")
            {
                Details = counts,
            },
            cancellationToken);
        await activity.FlushAsync(cancellationToken);

        return new ResetDataResultDto(
            counts.Patients, counts.Orders, counts.Payments, counts.LabTests, counts.ActivityLogs, counts.SystemErrors);
    }
}
