using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Application.Interfaces;

public interface IBackupRepository
{
    /// <summary>Newest first. The search term is not used: the history is short.</summary>
    Task<(IReadOnlyList<BackupRecordDto> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    /// <summary>Null when missing (no tracking).</summary>
    Task<BackupRecordDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    void Add(BackupRecord record);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
