using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class ClinicSettingsRepository(AppDbContext db) : IClinicSettingsRepository
{
    public Task<ClinicSettings> GetAsync(CancellationToken cancellationToken) =>
        db.Set<ClinicSettings>().SingleAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
