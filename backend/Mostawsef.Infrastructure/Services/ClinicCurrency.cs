using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Services;

/// <summary>Reads the single clinic currency once and keeps it until <see cref="Invalidate"/>.</summary>
public sealed class ClinicCurrency(IDbContextFactory<AppDbContext> dbFactory) : IClinicCurrency
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _code;

    public async Task<string> GetCodeAsync(CancellationToken cancellationToken)
    {
        if (_code is { } cached)
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_code is { } again)
            {
                return again;
            }

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            _code = await db.Set<ClinicSettings>().AsNoTracking()
                .Select(s => s.CurrencyCode)
                .SingleAsync(cancellationToken);
            return _code;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => _code = null;
}
