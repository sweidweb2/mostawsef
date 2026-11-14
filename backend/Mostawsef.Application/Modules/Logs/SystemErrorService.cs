using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;
using Mostawsef.Domain.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Modules.Logs;

public interface ISystemErrorService
{
    Task<CursorPage<SystemErrorListItemDto>> GetPageAsync(PageRequest request, CancellationToken cancellationToken);

    Task<SystemErrorDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<SystemErrorStatsDto> GetStatsAsync(CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Deletes the named errors (missing ids are skipped) and records the deletion as an activity.</summary>
    Task<DeleteLogsResultDto> DeleteManyAsync(DeleteLogsRequest request, CancellationToken cancellationToken);

    /// <summary>Deletes every error and records the clearing as an activity.</summary>
    Task<DeleteLogsResultDto> DeleteAllAsync(CancellationToken cancellationToken);

    /// <summary>Stores an error reported by the desktop app.</summary>
    Task ReportClientErrorAsync(ReportClientErrorRequest request, CancellationToken cancellationToken);
}

public class SystemErrorService(ISystemErrorRepository errors, ISystemErrorLogger errorLogger, IActivityLogger activity, TimeProvider time)
    : ISystemErrorService
{
    public const string DesktopSource = "Desktop";

    /// <summary>Longest kept value of the free-text details an anonymous client report can carry.</summary>
    private const int ClientDetailMaxLength = 200;

    public async Task<CursorPage<SystemErrorListItemDto>> GetPageAsync(PageRequest request, CancellationToken cancellationToken)
    {
        request.EnsureSearchable();
        return (await errors.GetPageAsync(request, cancellationToken)).ToResult(
            request, e => new SystemErrorListItemDto(
                e.Id, e.OccurredAt, e.Level.ToString(), e.Source, e.Message, e.RequestPath, e.StatusCode, e.Username));
    }

    public async Task<SystemErrorDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var e = await errors.GetByIdAsync(id, cancellationToken);
        return e is null
            ? null
            : new SystemErrorDto(
                e.Id, e.OccurredAt, e.Level.ToString(), e.Source, e.Message, e.ExceptionType, e.StackTrace,
                e.RequestPath, e.RequestMethod, e.StatusCode, e.UserId, e.Username, e.TraceId, e.IpAddress,
                e.Details);
    }

    public async Task<SystemErrorStatsDto> GetStatsAsync(CancellationToken cancellationToken)
    {
        var (todayStart, weekStart) = LogPeriods.Current(time);
        var counts = await errors.GetCountsAsync(todayStart, weekStart, cancellationToken);
        return new SystemErrorStatsDto(counts.Total, counts.Today, counts.Last7Days, counts.Extra);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await errors.DeleteAsync(id, cancellationToken))
        {
            return false;
        }

        await activity.LogAsync(
            new ActivityEntry(ActivityActions.SystemErrorDeleted, ActivityModules.Logs, "Deleted a system error record")
            {
                EntityType = nameof(SystemError),
                EntityId = id.ToString(),
            },
            cancellationToken);

        return true;
    }

    public async Task<DeleteLogsResultDto> DeleteManyAsync(DeleteLogsRequest request, CancellationToken cancellationToken)
    {
        var deleted = await errors.DeleteManyAsync(request.Ids.Distinct().ToList(), cancellationToken);
        if (deleted > 0)
        {
            await activity.LogAsync(
                new ActivityEntry(ActivityActions.SystemErrorsDeleted, ActivityModules.Logs, $"Deleted {deleted} system error records")
                {
                    Details = new { Deleted = deleted },
                },
                cancellationToken);
        }

        return new DeleteLogsResultDto(deleted);
    }

    public async Task<DeleteLogsResultDto> DeleteAllAsync(CancellationToken cancellationToken)
    {
        var deleted = await errors.DeleteAllAsync(cancellationToken);
        await activity.LogAsync(
            new ActivityEntry(ActivityActions.SystemErrorsCleared, ActivityModules.Logs, $"Deleted all system error records ({deleted})")
            {
                Details = new { Deleted = deleted },
            },
            cancellationToken);

        return new DeleteLogsResultDto(deleted);
    }

    public Task ReportClientErrorAsync(ReportClientErrorRequest request, CancellationToken cancellationToken)
    {
        var level = Enum.TryParse<ErrorLevel>(request.Level, ignoreCase: true, out var parsed) ? parsed : ErrorLevel.Error;

        return errorLogger.LogAsync(
            new SystemErrorEntry(level, DesktopSource, request.Message)
            {
                ExceptionType = request.ExceptionType,
                StackTrace = request.StackTrace,
                Details = new
                {
                    Context = Text.Truncate(request.Context, ClientDetailMaxLength),
                    AppVersion = Text.Truncate(request.AppVersion, ClientDetailMaxLength),
                    OsVersion = Text.Truncate(request.OsVersion, ClientDetailMaxLength),
                },
                IncludeRequestInfo = false,
            },
            cancellationToken);
    }
}
