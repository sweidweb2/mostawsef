using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Settings;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class ClinicSettingsRepository(AppDbContext db) : IClinicSettingsRepository
{
    public Task<ClinicSettingsDto> GetSnapshotAsync(CancellationToken cancellationToken) =>
        db.Set<ClinicSettings>().AsNoTracking()
            .Select(s => new ClinicSettingsDto(
                s.Name, s.Address, s.Phone, s.CurrencyCode, s.Version, s.UpdatedAt, s.UpdatedBy))
            .SingleAsync(cancellationToken);

    public Task<ClinicSettings> GetAsync(CancellationToken cancellationToken) =>
        db.Set<ClinicSettings>().SingleAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
