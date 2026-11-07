using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

/// <summary>
/// Increment-then-read inside a transaction: the UPDATE locks the row, so parallel callers queue and each reads its own value.
/// Uses a context of its own, so it never joins (or commits) the caller's unit of work.
/// </summary>
public class NumberSequenceRepository(IDbContextFactory<AppDbContext> dbFactory) : INumberSequence
{
    public async Task<long> NextAsync(string name, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var sequence = db.Set<NumberSequence>().Where(s => s.Name == name);
            if (await sequence.ExecuteUpdateAsync(s => s.SetProperty(x => x.NextValue, x => x.NextValue + 1), cancellationToken) > 0)
            {
                var next = await sequence.Select(s => s.NextValue).SingleAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return next - 1;
            }

            try
            {
                db.Add(new NumberSequence(name, 2));
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return 1;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // Another caller created the row first: take the next number from it.
            }
        }
    }
}
