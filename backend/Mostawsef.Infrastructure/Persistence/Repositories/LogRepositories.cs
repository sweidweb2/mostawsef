using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class ActivityLogRepository(AppDbContext db) : IActivityLogRepository
{
    public Task<KeysetPage<ActivityLogSummary>> GetPageAsync(PageRequest request, CancellationToken cancellationToken) =>
        db.ActivityLogs.AsNoTracking()
            .Matching(request.Search)
            .ToKeysetPageAsync(
                request,
                l => new ActivityLogSummary(l.Id, l.OccurredAt, l.Action, l.Module, l.Description, l.Username),
                l => new PageCursor(l.OccurredAt, l.Id),
                cancellationToken);

    public Task<ActivityLogDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.ActivityLogs.AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new ActivityLogDetails(
                l.Id, l.OccurredAt, l.Action, l.Module, l.Description, l.UserId, l.Username,
                l.EntityType, l.EntityId, l.IpAddress, l.UserAgent, l.Details))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        db.ActivityLogs.Where(l => ids.Contains(l.Id)).ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteAllAsync(CancellationToken cancellationToken) =>
        Retention.DeleteInBatchesAsync(db.ActivityLogs.OrderBy(l => l.OccurredAt), cancellationToken);

    public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken) =>
        Retention.DeleteInBatchesAsync(
            db.ActivityLogs.Where(l => l.OccurredAt < cutoffUtc).OrderBy(l => l.OccurredAt), cancellationToken);

    /// <summary>
    /// One statement of four COUNT subqueries, each served by an index: the total and the periods by (OccurredAt, Id),
    /// failed sign-ins by (Action, OccurredAt). An empty table returns no row, which means all zeros.
    /// </summary>
    public async Task<LogCounts> GetCountsAsync(DateTime todayStartUtc, DateTime weekStartUtc, CancellationToken cancellationToken)
    {
        var logs = db.ActivityLogs.AsNoTracking();
        return await logs
            .Select(_ => new LogCounts(
                logs.Count(),
                logs.Count(l => l.OccurredAt >= todayStartUtc),
                logs.Count(l => l.OccurredAt >= weekStartUtc),
                logs.Count(l => l.Action == ActivityActions.SignInFailed && l.OccurredAt >= todayStartUtc)))
            .FirstOrDefaultAsync(cancellationToken)
            ?? LogCounts.None;
    }
}

public class SystemErrorRepository(AppDbContext db) : ISystemErrorRepository
{
    public Task<KeysetPage<SystemErrorSummary>> GetPageAsync(PageRequest request, CancellationToken cancellationToken) =>
        db.SystemErrors.AsNoTracking()
            .Matching(request.Search)
            .ToKeysetPageAsync(
                request,
                e => new SystemErrorSummary(e.Id, e.OccurredAt, e.Level, e.Source, e.Message, e.RequestPath, e.StatusCode, e.Username),
                e => new PageCursor(e.OccurredAt, e.Id),
                cancellationToken);

    public Task<SystemErrorDetails?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.SystemErrors.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new SystemErrorDetails(
                e.Id, e.OccurredAt, e.Level, e.Source, e.Message, e.ExceptionType, e.StackTrace,
                e.RequestPath, e.RequestMethod, e.StatusCode, e.UserId, e.Username, e.TraceId, e.IpAddress, e.Details))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await db.SystemErrors.Where(e => e.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    public Task<int> DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        db.SystemErrors.Where(e => ids.Contains(e.Id)).ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteAllAsync(CancellationToken cancellationToken) =>
        Retention.DeleteInBatchesAsync(db.SystemErrors.OrderBy(e => e.OccurredAt), cancellationToken);

    public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken) =>
        Retention.DeleteInBatchesAsync(
            db.SystemErrors.Where(e => e.OccurredAt < cutoffUtc).OrderBy(e => e.OccurredAt), cancellationToken);

    /// <summary>
    /// One statement of four COUNT subqueries, each served by an index: the total and the periods by (OccurredAt, Id),
    /// critical errors by (Level, OccurredAt). An empty table returns no row, which means all zeros.
    /// </summary>
    public async Task<LogCounts> GetCountsAsync(DateTime todayStartUtc, DateTime weekStartUtc, CancellationToken cancellationToken)
    {
        var errors = db.SystemErrors.AsNoTracking();
        return await errors
            .Select(_ => new LogCounts(
                errors.Count(),
                errors.Count(e => e.OccurredAt >= todayStartUtc),
                errors.Count(e => e.OccurredAt >= weekStartUtc),
                errors.Count(e => e.Level == ErrorLevel.Critical)))
            .FirstOrDefaultAsync(cancellationToken)
            ?? LogCounts.None;
    }
}
