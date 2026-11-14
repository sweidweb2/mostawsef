using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Mostawsef.Application.Interfaces;
using Mostawsef.Domain.Enums;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Services.Backup;

/// <summary>
/// Exports the tables of the EF model straight from the database (plain SELECTs, so soft-deleted rows are included).
/// Reads everything in one repeatable-read transaction, so the tables of one export agree with each other, and writes
/// into a temporary file that is deleted when the returned stream is disposed.
/// </summary>
internal sealed class DatabaseBackupExporter(IDbContextFactory<AppDbContext> factory, TimeProvider time) : IBackupExporter
{
    private readonly Lazy<IReadOnlyList<TableInfo>> _tables = new(() =>
    {
        using var db = factory.CreateDbContext();
        return TableInfo.ReadModel(db.Model);
    });

    private IReadOnlyList<string>? _names;

    public IReadOnlyList<string> Tables => _names ??= _tables.Value.Select(t => t.Name).Order(StringComparer.Ordinal).ToList();

    public async Task<Stream> ExportAsync(string? table, BackupFormat format, CancellationToken cancellationToken)
    {
        IReadOnlyList<TableInfo> selected = table is null
            ? _tables.Value
            : [_tables.Value.FirstOrDefault(t => t.Name == table) ?? throw new ArgumentException("Unknown table.", nameof(table))];

        var file = new FileStream(
            Path.Combine(Path.GetTempPath(), $"mostawsef-backup-{Guid.NewGuid():N}.tmp"),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            var connection = db.Database.GetDbConnection();

            await using (var sink = BackupSinks.Create(format, file, allTables: table is null))
            {
                await sink.BeginAsync(time.GetLocalNow());
                foreach (var info in selected)
                {
                    await WriteTableAsync(sink, info, connection, transaction.GetDbTransaction(), cancellationToken);
                }

                await sink.EndAsync();
            }

            await file.FlushAsync(cancellationToken);
            file.Position = 0;
            return file;
        }
        catch
        {
            await file.DisposeAsync();
            throw;
        }
    }

    private static async Task WriteTableAsync(
        IBackupSink sink, TableInfo table, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
    {
        await sink.BeginTableAsync(table);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {table.QuotedColumns} FROM {Sql.Quote(table.Name)}";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var values = new object[table.Columns.Count];
        while (await reader.ReadAsync(cancellationToken))
        {
            reader.GetValues(values);
            var row = new Cell[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                row[i] = Cell.From(values[i], table.Columns[i]);
            }

            await sink.WriteRowAsync(row);
        }

        await sink.EndTableAsync();
    }
}
