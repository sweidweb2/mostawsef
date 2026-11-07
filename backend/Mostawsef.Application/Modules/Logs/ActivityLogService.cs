using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Modules.Logs;

public interface IActivityLogService
{
    Task<CursorPage<ActivityLogListItemDto>> GetPageAsync(PageRequest request, CancellationToken cancellationToken);

    Task<ActivityLogDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ActivityLogStatsDto> GetStatsAsync(CancellationToken cancellationToken);

    /// <summary>Returns false when the record does not exist. The deletion itself is recorded as an activity.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public class ActivityLogService(IActivityLogRepository logs, IActivityLogger activity, TimeProvider time) : IActivityLogService
{
    public async Task<CursorPage<ActivityLogListItemDto>> GetPageAsync(PageRequest request, CancellationToken cancellationToken)
    {
        request.EnsureSearchable();
        return (await logs.GetPageAsync(request, cancellationToken)).ToResult(
            request, l => new ActivityLogListItemDto(l.Id, l.OccurredAt, l.Action, l.Module, l.Description, l.Username));
    }

    public async Task<ActivityLogDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var log = await logs.GetByIdAsync(id, cancellationToken);
        return log is null ? null : ToDto(log);
    }

    public async Task<ActivityLogStatsDto> GetStatsAsync(CancellationToken cancellationToken)
    {
        var (todayStart, weekStart) = LogPeriods.Current(time);
        var counts = await logs.GetCountsAsync(todayStart, weekStart, cancellationToken);
        return new ActivityLogStatsDto(counts.Total, counts.Today, counts.Last7Days, counts.Extra);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var log = await logs.DeleteAsync(id, cancellationToken);
        if (log is null)
        {
            return false;
        }

        await activity.LogAsync(
            new ActivityEntry(ActivityActions.ActivityLogDeleted, ActivityModules.Logs, "Deleted an activity log record")
            {
                EntityType = nameof(ActivityLog),
                EntityId = id.ToString(),
                Details = new { log.Action, log.Username, log.OccurredAt },
            },
            cancellationToken);

        return true;
    }

    private static ActivityLogDto ToDto(ActivityLog l) => new(
        l.Id, l.OccurredAt, l.Action, l.Module, l.Description, l.UserId, l.Username,
        l.EntityType, l.EntityId, l.IpAddress, l.UserAgent, l.Details);
}

internal static class LogPeriods
{
    /// <summary>Start of today and of the last 7 days (server local time, as UTC).</summary>
    public static (DateTime TodayStartUtc, DateTime WeekStartUtc) Current(TimeProvider time)
    {
        var now = time.GetLocalNow();
        var today = new DateTimeOffset(now.Date, now.Offset);
        return (today.UtcDateTime, today.AddDays(-6).UtcDateTime);
    }
}
