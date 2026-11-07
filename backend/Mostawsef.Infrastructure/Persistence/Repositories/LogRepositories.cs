using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class ActivityLogRepository(AppDbContext db) : IActivityLogRepository
{
    public Task<KeysetPage<ActivityLog>> GetPageAsync(PageRequest request, CancellationToken cancellationToken) =>
        db.ActivityLogs.AsNoTracking()
            .Matching(request.Search)
            .ToKeysetPageAsync(request, l => l, l => new PageCursor(l.OccurredAt, l.Id), cancellationToken);

    public Task<ActivityLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.ActivityLogs.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<DeletedActivityLog?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var record = db.ActivityLogs.Where(l => l.Id == id);
        var deleted = await record
            .Select(l => new DeletedActivityLog(l.Action, l.Username, l.OccurredAt))
            .FirstOrDefaultAsync(cancellationToken);

        return deleted is not null && await record.ExecuteDeleteAsync(cancellationToken) > 0 ? deleted : null;
    }

    public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken) =>
        Retention.DeleteInBatchesAsync(
            db.ActivityLogs.Where(l => l.OccurredAt < cutoffUtc).OrderBy(l => l.OccurredAt), cancellationToken);

    /// <summary>One query, one pass over the table: each number is a filtered COUNT of the same scan.</summary>
    public async Task<LogCounts> GetCountsAsync(DateTime todayStartUtc, DateTime weekStartUtc, CancellationToken cancellationToken) =>
        await db.ActivityLogs.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new LogCounts(
                g.Count(),
                g.Count(l => l.OccurredAt >= todayStartUtc),
                g.Count(l => l.OccurredAt >= weekStartUtc),
                g.Count(l => l.OccurredAt >= todayStartUtc && l.Action == ActivityActions.SignInFailed)))
            .FirstOrDefaultAsync(cancellationToken)
        ?? LogCounts.None;
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

    public Task<SystemError?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.SystemErrors.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<DeletedSystemError?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var record = db.SystemErrors.Where(e => e.Id == id);
        var deleted = await record
            .Select(e => new DeletedSystemError(e.Source, e.Level, e.OccurredAt))
            .FirstOrDefaultAsync(cancellationToken);

        return deleted is not null && await record.ExecuteDeleteAsync(cancellationToken) > 0 ? deleted : null;
    }

    public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken) =>
        Retention.DeleteInBatchesAsync(
            db.SystemErrors.Where(e => e.OccurredAt < cutoffUtc).OrderBy(e => e.OccurredAt), cancellationToken);

    /// <summary>One query, one pass over the table: each number is a filtered COUNT of the same scan.</summary>
    public async Task<LogCounts> GetCountsAsync(DateTime todayStartUtc, DateTime weekStartUtc, CancellationToken cancellationToken) =>
        await db.SystemErrors.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new LogCounts(
                g.Count(),
                g.Count(e => e.OccurredAt >= todayStartUtc),
                g.Count(e => e.OccurredAt >= weekStartUtc),
                g.Count(e => e.Level == ErrorLevel.Critical)))
            .FirstOrDefaultAsync(cancellationToken)
        ?? LogCounts.None;
}
