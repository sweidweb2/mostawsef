using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Settings;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Services;

/// <summary>Reads the single clinic-settings row once and keeps it until <see cref="Invalidate"/>.</summary>
public sealed class ClinicSettingsCache(IDbContextFactory<AppDbContext> dbFactory) : IClinicSettingsCache
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ClinicSettingsDto? _cached;

    public async Task<ClinicSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        if (_cached is { } hit)
        {
            return hit;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cached is { } again)
            {
                return again;
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            _cached = await db.Set<ClinicSettings>().AsNoTracking()
                .Select(s => new ClinicSettingsDto(
                    s.Name, s.Address, s.Phone, s.CurrencyCode, s.Version, s.UpdatedAt, s.UpdatedBy))
                .SingleAsync(cancellationToken);
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> GetCurrencyCodeAsync(CancellationToken cancellationToken) =>
        (await GetAsync(cancellationToken)).CurrencyCode;

    public void Invalidate() => _cached = null;
}
