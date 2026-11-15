using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class ScheduledDumpRepository(AppDbContext db) : IScheduledDumpRepository
{
    public async Task<(IReadOnlyList<ScheduledDumpDto> Items, int Total)> GetPageAsync(
        PageQuery query, CancellationToken cancellationToken)
    {
        var records = db.ScheduledDumps.AsNoTracking();

        var rows = records
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id)
            .Select(d => new ScheduledDumpDto(
                d.Id,
                d.FolderPath,
                d.FilePath,
                d.FileSizeBytes,
                d.KeepCount,
                d.IntervalDays,
                d.DeletedOldFiles,
                d.CreatedAt));

        return await rows.ToPageAsync(records, query, cancellationToken);
    }

    public void Add(ScheduledDump dump) => db.ScheduledDumps.Add(dump);

    public async Task TrimToAsync(int keepCount, CancellationToken cancellationToken)
    {
        keepCount = Math.Max(1, keepCount);
        var oldIds = await db.ScheduledDumps
            .OrderByDescending(d => d.CreatedAt)
            .ThenByDescending(d => d.Id)
            .Skip(keepCount)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        if (oldIds.Count == 0)
        {
            return;
        }

        await db.ScheduledDumps.Where(d => oldIds.Contains(d.Id)).ExecuteDeleteAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
