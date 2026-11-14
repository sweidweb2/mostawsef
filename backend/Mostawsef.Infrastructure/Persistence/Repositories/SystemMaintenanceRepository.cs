using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class SystemMaintenanceRepository(AppDbContext db) : ISystemMaintenanceRepository
{
    /// <summary>The tables a data reset empties, children before parents. Users, settings and backup records are not listed.</summary>
    private static readonly Type[] ResetTables =
    [
        typeof(Payment), typeof(OrderItem), typeof(Order), typeof(Patient), typeof(LabTest),
        typeof(ActivityLog), typeof(SystemError), typeof(NumberSequence),
    ];

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

            // Soft-deleted patients go too: count them without the "not deleted" filter.
            var counts = new DataResetCounts(
                await db.Patients.IgnoreQueryFilters().CountAsync(cancellationToken),
                await db.Orders.IgnoreQueryFilters().CountAsync(cancellationToken),
                await db.Set<Payment>().IgnoreQueryFilters().CountAsync(cancellationToken),
                await db.LabTests.IgnoreQueryFilters().CountAsync(cancellationToken),
                await db.ActivityLogs.CountAsync(cancellationToken),
                await db.SystemErrors.CountAsync(cancellationToken));

            // One TRUNCATE for every table: fast on any size and no foreign key order to respect. Without CASCADE it
            // fails (and rolls back) rather than silently emptying a table that is not listed, such as users.
            var tables = string.Join(", ", ResetTables.Select(TableName));
#pragma warning disable EF1002 // Table names come from the EF model, never from input.
            await db.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {tables}", cancellationToken);
#pragma warning restore EF1002

            await transaction.CommitAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return counts;
        });
    }

    private string TableName(Type type)
    {
        var entity = db.Model.FindEntityType(type) ?? throw new InvalidOperationException($"{type.Name} is not mapped.");
        var schema = entity.GetSchema();
        var table = entity.GetTableName() ?? throw new InvalidOperationException($"{type.Name} has no table.");
        return schema is null ? $"\"{table}\"" : $"\"{schema}\".\"{table}\"";
    }

    private static List<HourlyValue> Hours(IEnumerable<HourRow> rows) =>
        rows.Select(r => new HourlyValue(new DateTime(r.Year, r.Month, r.Day, r.Hour, 0, 0, DateTimeKind.Utc), r.Value)).ToList();

    private sealed record HourRow(int Year, int Month, int Day, int Hour, long Value);
}
