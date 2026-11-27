using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

/// <summary>
/// One UPDATE ... RETURNING on the request's <see cref="AppDbContext"/>: when the caller already opened a
/// transaction (order create), the bump rolls back with that work so a failed save leaves no number gap.
/// Outside a transaction the statement still locks the row so parallel callers queue. Only the very first
/// number of a sequence (no row yet) inserts the row, joining the ambient transaction when there is one.
/// </summary>
public class NumberSequenceRepository(AppDbContext db) : INumberSequence
{
    public async Task<long> NextAsync(string name, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (await TakeNextAsync(db, name, cancellationToken) is { } taken)
            {
                return taken;
            }

            var ownsTransaction = db.Database.CurrentTransaction is null;
            await using var transaction = ownsTransaction
                ? await db.Database.BeginTransactionAsync(cancellationToken)
                : null;

            try
            {
                db.Set<NumberSequence>().Add(new NumberSequence(name, 2));
                await db.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return 1;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // Another caller created the row first: take the next number from it.
                db.ChangeTracker.Clear();
            }
            catch (AppException ex) when (attempt == 0 && ex.Code == ErrorCodes.Taken)
            {
                // SaveChanges maps the unique race to AppException — same retry.
                db.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>The current number of an existing sequence, moving it on by one in the same statement; null when the sequence has no row yet.</summary>
    private static async Task<long?> TakeNextAsync(AppDbContext db, string name, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE number_sequences SET \"NextValue\" = \"NextValue\" + 1 WHERE \"Name\" = @name RETURNING \"NextValue\" - 1";
        if (db.Database.CurrentTransaction is { } current)
        {
            command.Transaction = current.GetDbTransaction();
        }

        var parameter = command.CreateParameter();
        parameter.ParameterName = "name";
        parameter.Value = name;
        command.Parameters.Add(parameter);
        return await command.ExecuteScalarAsync(cancellationToken) as long?;
    }
}
