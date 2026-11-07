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
    public async Task<(IReadOnlyList<ActivityLog> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var logs = db.ActivityLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            logs = logs.Where(l =>
                (l.Username != null && l.Username.ToLower().Contains(term)) ||
                l.Action.ToLower().Contains(term) ||
                l.Module.ToLower().Contains(term) ||
                l.Description.ToLower().Contains(term));
        }

        var total = await logs.CountAsync(cancellationToken);
        var items = await logs
            .OrderByDescending(l => l.OccurredAt)
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<ActivityLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.ActivityLogs.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await db.ActivityLogs.Where(l => l.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task<LogCounts> GetCountsAsync(DateTime todayStartUtc, DateTime weekStartUtc, CancellationToken cancellationToken)
    {
        var logs = db.ActivityLogs.AsNoTracking();
        return new LogCounts(
            await logs.CountAsync(cancellationToken),
            await logs.CountAsync(l => l.OccurredAt >= todayStartUtc, cancellationToken),
            await logs.CountAsync(l => l.OccurredAt >= weekStartUtc, cancellationToken),
            await logs.CountAsync(l => l.OccurredAt >= todayStartUtc && l.Action == ActivityActions.SignInFailed, cancellationToken));
    }
}

public class SystemErrorRepository(AppDbContext db) : ISystemErrorRepository
{
    public async Task<(IReadOnlyList<SystemError> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var errors = db.SystemErrors.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            errors = errors.Where(e =>
                e.Message.ToLower().Contains(term) ||
                e.Source.ToLower().Contains(term) ||
                (e.ExceptionType != null && e.ExceptionType.ToLower().Contains(term)) ||
                (e.RequestPath != null && e.RequestPath.ToLower().Contains(term)) ||
                (e.Username != null && e.Username.ToLower().Contains(term)));
        }

        var total = await errors.CountAsync(cancellationToken);
        var items = await errors
            .OrderByDescending(e => e.OccurredAt)
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<SystemError?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.SystemErrors.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await db.SystemErrors.Where(e => e.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task<LogCounts> GetCountsAsync(DateTime todayStartUtc, DateTime weekStartUtc, CancellationToken cancellationToken)
    {
        var errors = db.SystemErrors.AsNoTracking();
        return new LogCounts(
            await errors.CountAsync(cancellationToken),
            await errors.CountAsync(e => e.OccurredAt >= todayStartUtc, cancellationToken),
            await errors.CountAsync(e => e.OccurredAt >= weekStartUtc, cancellationToken),
            await errors.CountAsync(e => e.Level == ErrorLevel.Critical, cancellationToken));
    }
}
