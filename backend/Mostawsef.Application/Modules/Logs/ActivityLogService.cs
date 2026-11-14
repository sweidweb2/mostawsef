using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;

namespace Mostawsef.Application.Modules.Logs;

public interface IActivityLogService
{
    Task<CursorPage<ActivityLogListItemDto>> GetPageAsync(PageRequest request, CancellationToken cancellationToken);

    Task<ActivityLogDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ActivityLogStatsDto> GetStatsAsync(CancellationToken cancellationToken);

    /// <summary>Deletes the named records; the deletion itself is then recorded as a new activity.</summary>
    Task<DeleteLogsResultDto> DeleteManyAsync(DeleteLogsRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes every record; the clearing itself is then recorded as the first new activity.</summary>
    Task<DeleteLogsResultDto> DeleteAllAsync(CancellationToken cancellationToken);
}

public class ActivityLogService(IActivityLogRepository logs, IActivityLogger activity, TimeProvider time) : IActivityLogService
{
    public async Task<DeleteLogsResultDto> DeleteManyAsync(DeleteLogsRequest request, CancellationToken cancellationToken)
    {
        var deleted = await logs.DeleteManyAsync(request.Ids.Distinct().ToList(), cancellationToken);
        if (deleted > 0)
        {
            await activity.LogAsync(
                new ActivityEntry(ActivityActions.ActivityLogsDeleted, ActivityModules.Logs, $"Deleted {deleted} activity log records")
                {
                    Details = new { Deleted = deleted },
                },
                cancellationToken);
        }

        return new DeleteLogsResultDto(deleted);
    }

    public async Task<DeleteLogsResultDto> DeleteAllAsync(CancellationToken cancellationToken)
    {
        var deleted = await logs.DeleteAllAsync(cancellationToken);
        await activity.LogAsync(
            new ActivityEntry(ActivityActions.ActivityLogsCleared, ActivityModules.Logs, $"Deleted all activity log records ({deleted})")
            {
                Details = new { Deleted = deleted },
            },
            cancellationToken);

        return new DeleteLogsResultDto(deleted);
    }

    public async Task<CursorPage<ActivityLogListItemDto>> GetPageAsync(PageRequest request, CancellationToken cancellationToken)
    {
        request.EnsureSearchable();
        return (await logs.GetPageAsync(request, cancellationToken)).ToResult(
            request, l => new ActivityLogListItemDto(l.Id, l.OccurredAt, l.Action, l.Module, l.Description, l.Username));
    }

    public async Task<ActivityLogDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var log = await logs.GetByIdAsync(id, cancellationToken);
        return log is null
            ? null
            : new ActivityLogDto(
                log.Id, log.OccurredAt, log.Action, log.Module, log.Description, log.UserId, log.Username,
                log.EntityType, log.EntityId, log.IpAddress, log.UserAgent, log.Details);
    }

    public async Task<ActivityLogStatsDto> GetStatsAsync(CancellationToken cancellationToken)
    {
        var (todayStart, weekStart) = LogPeriods.Current(time);
        var counts = await logs.GetCountsAsync(todayStart, weekStart, cancellationToken);
        return new ActivityLogStatsDto(counts.Total, counts.Today, counts.Last7Days, counts.Extra);
    }
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
