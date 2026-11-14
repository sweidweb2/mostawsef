using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Maintenance;
using Mostawsef.Contracts.Orders;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Modules.Maintenance;

public interface ISystemMaintenanceService
{
    /// <summary>Totals, orders per status and the last <paramref name="days"/> days (7 to <see cref="SystemMaintenanceService.MaxDays"/>), today last.</summary>
    Task<SuperDashboardDto> GetDashboardAsync(int days, CancellationToken cancellationToken);

    /// <summary>Empties every business and log table once the confirmation word matches; accounts and settings stay. Logged as an activity.</summary>
    Task<ResetDataResultDto> ResetDataAsync(ResetDataRequest request, CancellationToken cancellationToken);
}

public class SystemMaintenanceService(
    ISystemMaintenanceRepository repository,
    IOrderRepository orders,
    IClinicSettingsCache clinic,
    IActivityLogger activity,
    TimeProvider time) : ISystemMaintenanceService
{
    public const int MinDays = 7;
    public const int MaxDays = 90;

    public async Task<SuperDashboardDto> GetDashboardAsync(int days, CancellationToken cancellationToken)
    {
        days = Math.Clamp(days, MinDays, MaxDays);
        var zone = time.LocalTimeZone;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);
        var first = today.AddDays(1 - days);
        var sinceUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(first.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone);

        // One DbContext per request: the queries run one after another.
        var currency = await clinic.GetCurrencyCodeAsync(cancellationToken);
        var totals = await repository.GetTotalsAsync(cancellationToken);
        var statusCounts = await orders.GetStatusCountsAsync(cancellationToken);
        var series = await repository.GetHourlySeriesAsync(sinceUtc, cancellationToken);

        var orderDays = PerDay(series.Orders, zone);
        var patientDays = PerDay(series.Patients, zone);
        var revenueDays = PerDay(series.Revenue, zone);
        var activityDays = PerDay(series.Activity, zone);
        var errorDays = PerDay(series.Errors, zone);

        var dayList = Enumerable.Range(0, days)
            .Select(offset => first.AddDays(offset))
            .Select(date => new DashboardDayDto(
                date,
                (int)orderDays.GetValueOrDefault(date),
                (int)patientDays.GetValueOrDefault(date),
                revenueDays.GetValueOrDefault(date),
                (int)activityDays.GetValueOrDefault(date),
                (int)errorDays.GetValueOrDefault(date)))
            .ToList();

        var statuses = OrderStatuses.All
            .Select(s => new StatusCountDto(s, statusCounts.GetValueOrDefault(Enum.Parse<OrderStatus>(s))))
            .ToList();

        return new SuperDashboardDto(
            currency,
            totals.Users,
            totals.ActiveUsers,
            totals.Patients,
            totals.LabTests,
            totals.Orders,
            totals.Revenue,
            totals.Outstanding,
            dayList[^1].Activity,
            dayList[^1].Errors,
            statuses,
            dayList);
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

    private static Dictionary<DateOnly, long> PerDay(IReadOnlyList<HourlyValue> hours, TimeZoneInfo zone) =>
        hours
            .GroupBy(h => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(h.HourUtc, DateTimeKind.Utc), zone)))
            .ToDictionary(g => g.Key, g => g.Sum(h => h.Value));
}
