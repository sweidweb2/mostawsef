using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;

namespace Mostawsef.Application.Modules.Logs;

/// <summary>Bound from the "LogRetention" configuration section. 0 days keeps that kind of record forever.</summary>
public class LogRetentionOptions
{
    public const string SectionName = "LogRetention";

    [Range(0, 36500)]
    public int ActivityLogDays { get; set; } = 365;

    [Range(0, 36500)]
    public int SystemErrorDays { get; set; } = 90;
}

public record LogPurgeResult(int ActivityLogs, int SystemErrors);

public interface ILogRetentionService
{
    /// <summary>Deletes records older than their retention period. A purge that removed anything is recorded as an activity.</summary>
    Task<LogPurgeResult> PurgeExpiredAsync(CancellationToken cancellationToken);
}

public class LogRetentionService(
    IActivityLogRepository logs,
    ISystemErrorRepository errors,
    IActivityLogger activity,
    IOptions<LogRetentionOptions> options,
    TimeProvider time) : ILogRetentionService
{
    public async Task<LogPurgeResult> PurgeExpiredAsync(CancellationToken cancellationToken)
    {
        var retention = options.Value;
        var now = time.GetUtcNow().UtcDateTime;

        var result = new LogPurgeResult(
            retention.ActivityLogDays > 0
                ? await logs.DeleteOlderThanAsync(now.AddDays(-retention.ActivityLogDays), cancellationToken)
                : 0,
            retention.SystemErrorDays > 0
                ? await errors.DeleteOlderThanAsync(now.AddDays(-retention.SystemErrorDays), cancellationToken)
                : 0);

        if (result.ActivityLogs + result.SystemErrors > 0)
        {
            await activity.LogAsync(
                new ActivityEntry(ActivityActions.LogsPurged, ActivityModules.Logs, "Removed log records past their retention period")
                {
                    Details = new { result.ActivityLogs, result.SystemErrors, retention.ActivityLogDays, retention.SystemErrorDays },
                },
                cancellationToken);
            await activity.FlushAsync(cancellationToken);
        }

        return result;
    }
}
