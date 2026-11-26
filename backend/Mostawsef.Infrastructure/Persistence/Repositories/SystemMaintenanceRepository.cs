using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Persistence;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class SystemMaintenanceRepository(AppDbContext db) : ISystemMaintenanceRepository
{
    public async Task<DashboardTotals> GetTotalsAsync(CancellationToken cancellationToken)
    {
        // One statement with three COUNT subqueries (same shape as log stats); empty Users yields zeros.
        var users = db.Users.AsNoTracking();
        var tests = db.LabTests.AsNoTracking();
        return await users
            .Select(_ => new DashboardTotals(
                users.Count(),
                users.Count(u => u.IsActive),
                tests.Count()))
            .FirstOrDefaultAsync(cancellationToken)
            ?? new DashboardTotals(0, 0, 0);
    }

    public Task<long> GetOutstandingAsync(DateTime sinceUtc, DateTime untilUtc, CancellationToken cancellationToken) =>
        db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= sinceUtc && o.CreatedAt < untilUtc
                && (o.Status == OrderStatus.Pending || o.Status == OrderStatus.PartiallyPaid))
            .SumAsync(o => o.Total - o.Paid, cancellationToken);

    public async Task<DashboardHourlySeries> GetHourlySeriesAsync(DateTime sinceUtc, DateTime untilUtc, CancellationToken cancellationToken)
    {
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.CreatedAt >= sinceUtc && o.CreatedAt < untilUtc)
            .Select(o => o.CreatedAt)
            .CountPerHourAsync(cancellationToken);

        var patients = await db.Patients.AsNoTracking()
            .Where(p => p.CreatedAt >= sinceUtc && p.CreatedAt < untilUtc)
            .Select(p => p.CreatedAt)
            .CountPerHourAsync(cancellationToken);

        var revenue = HourlySeries.ToHours(await db.Set<Payment>().AsNoTracking()
            .Where(p => p.PaidAt >= sinceUtc && p.PaidAt < untilUtc)
            .GroupBy(p => new { p.PaidAt.Year, p.PaidAt.Month, p.PaidAt.Day, p.PaidAt.Hour })
            .Select(g => new HourlySeries.HourRow(
                g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour,
                g.Sum(p => p.Type == PaymentType.Refund ? -p.Amount : p.Amount)))
            .ToListAsync(cancellationToken));

        var activity = await db.ActivityLogs.AsNoTracking()
            .Where(l => l.OccurredAt >= sinceUtc && l.OccurredAt < untilUtc)
            .Select(l => l.OccurredAt)
            .CountPerHourAsync(cancellationToken);

        var errors = await db.SystemErrors.AsNoTracking()
            .Where(e => e.OccurredAt >= sinceUtc && e.OccurredAt < untilUtc)
            .Select(e => e.OccurredAt)
            .CountPerHourAsync(cancellationToken);

        return new DashboardHourlySeries(orders, patients, revenue, activity, errors);
    }

    /// <summary>Five MIN lookups, each answered from an index on the time column.</summary>
    public async Task<DateTime?> GetFirstRecordAtAsync(CancellationToken cancellationToken)
    {
        DateTime?[] firsts =
        [
            await db.Orders.AsNoTracking().MinAsync(o => (DateTime?)o.CreatedAt, cancellationToken),
            await db.Patients.AsNoTracking().MinAsync(p => (DateTime?)p.CreatedAt, cancellationToken),
            await db.Set<Payment>().AsNoTracking().MinAsync(p => (DateTime?)p.PaidAt, cancellationToken),
            await db.ActivityLogs.AsNoTracking().MinAsync(l => (DateTime?)l.OccurredAt, cancellationToken),
            await db.SystemErrors.AsNoTracking().MinAsync(e => (DateTime?)e.OccurredAt, cancellationToken),
        ];
        return firsts.Min();
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
            // Keep one-time data-backfill markers so a later restart does not re-copy Total into Addon on new orders.
            await db.Set<NumberSequence>()
                .Where(s => s.Name != OrderAddonBackfill.CompletionMarker
                    && s.Name != PatientSearchTextRebuild.CompletionMarker)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return new DataResetCounts(patients, orders, payments, labTests, activityLogs, systemErrors);
        });
    }
}
