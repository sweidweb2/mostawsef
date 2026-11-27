using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mostawsef.Application.Interfaces;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

/// <summary>
/// One UPDATE ... RETURNING on the request's <see cref="AppDbContext"/> connection: when the caller already opened a
/// transaction (order create), the bump rolls back with that work so a failed save leaves no number gap.
/// Outside a transaction the statement still locks the row so parallel callers queue. The very first number of a
/// sequence inserts the row with INSERT ... ON CONFLICT DO NOTHING (a race never fails, so it never aborts the
/// caller's transaction). Plain SQL statements only: the caller's tracked entities are never saved or cleared.
/// </summary>
public class NumberSequenceRepository(AppDbContext db) : INumberSequence
{
    private const string TakeSql =
        "UPDATE number_sequences SET \"NextValue\" = \"NextValue\" + 1 WHERE \"Name\" = @name RETURNING \"NextValue\" - 1";

    private const string CreateSql =
        "INSERT INTO number_sequences (\"Name\", \"NextValue\") VALUES (@name, 1) ON CONFLICT (\"Name\") DO NOTHING";

    public async Task<long> NextAsync(string name, CancellationToken cancellationToken)
    {
        if (await RunAsync(TakeSql, name, cancellationToken) is long taken)
        {
            return taken;
        }

        // No row yet: create it at 1 (or let the caller that won the race create it), then take from it as usual.
        await RunAsync(CreateSql, name, cancellationToken);
        return await RunAsync(TakeSql, name, cancellationToken) as long?
            ?? throw new InvalidOperationException($"Number sequence '{name}' could not be created.");
    }

    private async Task<object?> RunAsync(string sql, string name, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        var parameter = command.CreateParameter();
        parameter.ParameterName = "name";
        parameter.Value = name;
        command.Parameters.Add(parameter);
        return await command.ExecuteScalarAsync(cancellationToken);
    }
}
