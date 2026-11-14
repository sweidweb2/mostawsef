using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

/// <summary>
/// One UPDATE ... RETURNING: the statement locks the row, so parallel callers queue and each gets its own value, with no
/// explicit transaction. Only the very first number of a sequence (no row yet) inserts the row inside a transaction.
/// Uses a context of its own, so it never joins (or commits) the caller's unit of work.
/// </summary>
public class NumberSequenceRepository(IDbContextFactory<AppDbContext> dbFactory) : INumberSequence
{
    public async Task<long> NextAsync(string name, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            if (await TakeNextAsync(db, name, cancellationToken) is { } taken)
            {
                return taken;
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

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
            catch (AppException ex) when (attempt == 0 && ex.Code == ErrorCodes.Taken)
            {
                // SaveChanges maps the unique race to AppException — same retry.
            }
        }
    }

    /// <summary>The current number of an existing sequence, moving it on by one in the same statement; null when the sequence has no row yet.</summary>
    private static async Task<long?> TakeNextAsync(AppDbContext db, string name, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "UPDATE number_sequences SET \"NextValue\" = \"NextValue\" + 1 WHERE \"Name\" = @name RETURNING \"NextValue\" - 1";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "name";
        parameter.Value = name;
        command.Parameters.Add(parameter);
        return await command.ExecuteScalarAsync(cancellationToken) as long?;
    }
}
