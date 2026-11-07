using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Interfaces;

public record LogCounts(int Total, int Today, int Last7Days, int Extra);

public interface IActivityLogRepository
{
    /// <summary>Newest first. Search matches username, action, module or description.</summary>
    Task<(IReadOnlyList<ActivityLog> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    Task<ActivityLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Extra = failed sign-ins since <paramref name="todayStartUtc"/>.</summary>
    Task<LogCounts> GetCountsAsync(DateTime todayStartUtc, DateTime weekStartUtc, CancellationToken cancellationToken);
}

public interface ISystemErrorRepository
{
    /// <summary>Newest first. Search matches message, source, exception type, path or username.</summary>
    Task<(IReadOnlyList<SystemError> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    Task<SystemError?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Extra = critical errors (all time).</summary>
    Task<LogCounts> GetCountsAsync(DateTime todayStartUtc, DateTime weekStartUtc, CancellationToken cancellationToken);
}
