using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class SystemSettingsRepository(AppDbContext db) : ISystemSettingsRepository
{
    public Task<SystemSettings> GetAsync(CancellationToken cancellationToken) =>
        db.Set<SystemSettings>().SingleAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
