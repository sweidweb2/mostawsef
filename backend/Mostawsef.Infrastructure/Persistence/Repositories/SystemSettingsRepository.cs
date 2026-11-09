using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Settings;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class SystemSettingsRepository(AppDbContext db) : ISystemSettingsRepository
{
    public Task<SystemSettingsDto> GetSnapshotAsync(CancellationToken cancellationToken) =>
        db.Set<SystemSettings>().AsNoTracking()
            .Select(s => new SystemSettingsDto(s.BackupPath, s.MaintenanceMode, s.Version, s.UpdatedAt, s.UpdatedBy))
            .SingleAsync(cancellationToken);

    public Task<SystemSettings> GetForUpdateAsync(CancellationToken cancellationToken) =>
        db.Set<SystemSettings>().SingleAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
