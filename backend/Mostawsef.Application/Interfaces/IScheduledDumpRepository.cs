using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Interfaces;

public interface IScheduledDumpRepository
{
    /// <summary>Newest first. Search is unused: the history is browsed by date.</summary>
    Task<(IReadOnlyList<ScheduledDumpDto> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    void Add(ScheduledDump dump);

    /// <summary>Keeps the newest <paramref name="keepCount"/> rows (same cap as dump files on disk); deletes the rest.</summary>
    Task TrimToAsync(int keepCount, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
