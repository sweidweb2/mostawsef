using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class SystemMaintenanceRepository(AppDbContext db) : ISystemMaintenanceRepository
{
    public async Task<DashboardTotals> GetTotalsAsync(CancellationToken cancellationToken)
    {
        var users = await db.Users.CountAsync(cancellationToken);
        var activeUsers = await db.Users.CountAsync(u => u.IsActive, cancellationToken);
        var patients = await db.Patients.CountAsync(cancellationToken);
        var labTests = await db.LabTests.CountAsync(cancellationToken);
        var orders = await db.Orders.CountAsync(cancellationToken);
        var revenue = await db.Set<Payment>().AsNoTracking()
            .SumAsync(p => p.Type == PaymentType.Refund ? -p.Amount : p.Amount, cancellationToken);
        var outstanding = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.PartiallyPaid)
            .SumAsync(o => o.Total - o.Paid, cancellationToken);

        return new DashboardTotals(users, activeUsers, patients, labTests, orders, revenue, outstanding);
    }

    /// <summary>
    /// Each series is grouped by the UTC year/month/day/hour in the database (a few hundred rows at most), so the
    /// service can place every hour on its local calendar day without loading the individual rows.
    /// </summary>
    public async Task<DashboardHourlySeries> GetHourlySeriesAsync(DateTime sinceUtc, CancellationToken cancellationToken)
    {
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= sinceUtc)
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month, o.CreatedAt.Day, o.CreatedAt.Hour })
            .Select(g => new HourRow(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, g.Count()))
            .ToListAsync(cancellationToken);

        var patients = await db.Patients.AsNoTracking()
            .Where(p => p.CreatedAt >= sinceUtc)
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month, p.CreatedAt.Day, p.CreatedAt.Hour })
            .Select(g => new HourRow(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, g.Count()))
            .ToListAsync(cancellationToken);

        var revenue = await db.Set<Payment>().AsNoTracking()
            .Where(p => p.PaidAt >= sinceUtc)
            .GroupBy(p => new { p.PaidAt.Year, p.PaidAt.Month, p.PaidAt.Day, p.PaidAt.Hour })
            .Select(g => new HourRow(
                g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour,
                g.Sum(p => p.Type == PaymentType.Refund ? -p.Amount : p.Amount)))
            .ToListAsync(cancellationToken);

        var activity = await db.ActivityLogs.AsNoTracking()
            .Where(l => l.OccurredAt >= sinceUtc)
            .GroupBy(l => new { l.OccurredAt.Year, l.OccurredAt.Month, l.OccurredAt.Day, l.OccurredAt.Hour })
            .Select(g => new HourRow(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, g.Count()))
            .ToListAsync(cancellationToken);

        var errors = await db.SystemErrors.AsNoTracking()
            .Where(e => e.OccurredAt >= sinceUtc)
            .GroupBy(e => new { e.OccurredAt.Year, e.OccurredAt.Month, e.OccurredAt.Day, e.OccurredAt.Hour })
            .Select(g => new HourRow(g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, g.Count()))
            .ToListAsync(cancellationToken);

        return new DashboardHourlySeries(Hours(orders), Hours(patients), Hours(revenue), Hours(activity), Hours(errors));
    }

    public async Task<DataResetCounts> ResetDataAsync(CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // Plain LINQ deletes (portable, no raw SQL), children before parents so no foreign key blocks a step; each
            // returns its row count. IgnoreQueryFilters: soft-deleted rows go too.
            var payments = await db.Set<Payment>().IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
            await db.OrderItems.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
            var orders = await db.Orders.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
            var patients = await db.Patients.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
            var labTests = await db.LabTests.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
            var activityLogs = await db.ActivityLogs.ExecuteDeleteAsync(cancellationToken);
            var systemErrors = await db.SystemErrors.ExecuteDeleteAsync(cancellationToken);
            await db.Set<NumberSequence>().ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return new DataResetCounts(patients, orders, payments, labTests, activityLogs, systemErrors);
        });
    }

    private static List<HourlyValue> Hours(IEnumerable<HourRow> rows) =>
        rows.Select(r => new HourlyValue(new DateTime(r.Year, r.Month, r.Day, r.Hour, 0, 0, DateTimeKind.Utc), r.Value)).ToList();

    private sealed record HourRow(int Year, int Month, int Day, int Hour, long Value);
}
