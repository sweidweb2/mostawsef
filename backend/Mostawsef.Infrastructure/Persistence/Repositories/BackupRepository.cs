using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class BackupRepository(AppDbContext db) : IBackupRepository
{
    public async Task<(IReadOnlyList<BackupRecordDto> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var records = db.BackupRecords.AsNoTracking();

        var rows = records
            .OrderByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .Select(b => new BackupRecordDto(b.Id, b.Table, b.Format.ToString(), b.CreatedAt, b.CreatedBy));

        return await rows.ToPageAsync(records, query, cancellationToken);
    }

    public Task<BackupRecordDto?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.BackupRecords.AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new BackupRecordDto(b.Id, b.Table, b.Format.ToString(), b.CreatedAt, b.CreatedBy))
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(BackupRecord record) => db.BackupRecords.Add(record);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
