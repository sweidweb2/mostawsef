using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Infrastructure.Services.Backup;

/// <summary>Where the exporter sends rows: one implementation per format. Tables arrive one after another.</summary>
internal interface IBackupSink : IAsyncDisposable
{
    ValueTask BeginAsync(DateTimeOffset at);

    ValueTask BeginTableAsync(TableInfo table);

    ValueTask WriteRowAsync(Cell[] row);

    ValueTask EndTableAsync();

    ValueTask EndAsync();
}

internal static class BackupSinks
{
    public static IBackupSink Create(BackupFormat format, Stream output, bool allTables) => format switch
    {
        BackupFormat.Csv => new CsvSink(output, allTables),
        BackupFormat.Json => new JsonSink(output),
        BackupFormat.PostgreSql => new PostgreSqlSink(output),
        _ => new SqlSink(output),
    };

    public static StreamWriter Utf8Writer(Stream output) =>
        new(output, new UTF8Encoding(false), bufferSize: 64 * 1024, leaveOpen: true) { NewLine = "\n" };
}

/// <summary>
/// One CSV file for a table, or a ZIP with one CSV per table. UTF-8 with a BOM so Excel shows Arabic correctly.
/// Made to open in a spreadsheet: formula-like text is defused (<see cref="SpreadsheetSafe"/>).
/// </summary>
internal sealed class CsvSink(Stream output, bool allTables) : IBackupSink
{
    private readonly ZipArchive? _zip = allTables ? new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true) : null;
    private readonly StringBuilder _line = new();
    private StreamWriter? _writer;

    public ValueTask BeginAsync(DateTimeOffset at) => ValueTask.CompletedTask;

    public async ValueTask BeginTableAsync(TableInfo table)
    {
        var target = _zip is null ? output : _zip.CreateEntry(table.Name + ".csv", CompressionLevel.Optimal).Open();
        _writer = new StreamWriter(target, new UTF8Encoding(true), 64 * 1024, leaveOpen: _zip is null) { NewLine = "\r\n" };

        _line.Clear();
        foreach (var column in table.Columns)
        {
            Append(column.Name, quoteEmpty: false);
        }

        await _writer.WriteLineAsync(_line.ToString());
    }

    public async ValueTask WriteRowAsync(Cell[] row)
    {
        _line.Clear();
        foreach (var cell in row)
        {
            // An unquoted empty field is NULL and "" is an empty string, as PostgreSQL's COPY ... CSV reads them.
            Append(cell.Kind == CellKind.Text ? SpreadsheetSafe(cell.Text!) : cell.Text, quoteEmpty: cell.Kind != CellKind.Null);
        }

        await _writer!.WriteLineAsync(_line.ToString());
    }

    public async ValueTask EndTableAsync()
    {
        await _writer!.DisposeAsync();
        _writer = null;
    }

    public ValueTask EndAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_writer is not null)
        {
            await _writer.DisposeAsync();
        }

        _zip?.Dispose();
    }

    /// <summary>
    /// Text that a spreadsheet would run as a formula (=, +, -, @, tab, CR first: CSV injection) gets a leading
    /// apostrophe, so Excel shows it as typed. CSV is the spreadsheet format; SQL and PostgreSQL keep the exact text.
    /// </summary>
    internal static string SpreadsheetSafe(string text) =>
        text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + text : text;

    private void Append(string? value, bool quoteEmpty)
    {
        if (_line.Length > 0)
        {
            _line.Append(',');
        }

        if (string.IsNullOrEmpty(value))
        {
            if (value is not null && quoteEmpty)
            {
                _line.Append("\"\"");
            }

            return;
        }

        if (value.AsSpan().IndexOfAny(",\"\r\n") >= 0 || value[0] == ' ' || value[^1] == ' ')
        {
            _line.Append('"').Append(value.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
        }
        else
        {
            _line.Append(value);
        }
    }
}

/// <summary>{ "exportedAt": ..., "tables": { "patients": [ { "id": ..., ... } ] } }</summary>
internal sealed class JsonSink(Stream output) : IBackupSink
{
    private const int FlushEvery = 500;

    private readonly Utf8JsonWriter _json = new(output, new JsonWriterOptions
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keep Arabic readable instead of \uXXXX
    });

    private TableInfo? _table;
    private int _rows;

    public ValueTask BeginAsync(DateTimeOffset at)
    {
        _json.WriteStartObject();
        _json.WriteString("exportedAt", at);
        _json.WriteStartObject("tables");
        return ValueTask.CompletedTask;
    }

    public ValueTask BeginTableAsync(TableInfo table)
    {
        _table = table;
        _rows = 0;
        _json.WriteStartArray(table.Name);
        return ValueTask.CompletedTask;
    }

    public async ValueTask WriteRowAsync(Cell[] row)
    {
        _json.WriteStartObject();
        for (var i = 0; i < row.Length; i++)
        {
            var name = _table!.Columns[i].Name;
            var cell = row[i];
            switch (cell.Kind)
            {
                case CellKind.Null:
                    _json.WriteNull(name);
                    break;
                case CellKind.Bool:
                    _json.WriteBoolean(name, cell.Text == "true");
                    break;
                case CellKind.Number:
                    _json.WritePropertyName(name);
                    _json.WriteRawValue(cell.Text!);
                    break;
                default:
                    _json.WriteString(name, cell.Text);
                    break;
            }
        }

        _json.WriteEndObject();

        if (++_rows % FlushEvery == 0)
        {
            await _json.FlushAsync();
        }
    }

    public ValueTask EndTableAsync()
    {
        _json.WriteEndArray();
        return ValueTask.CompletedTask;
    }

    public async ValueTask EndAsync()
    {
        _json.WriteEndObject();
        _json.WriteEndObject();
        await _json.FlushAsync();
    }

    public ValueTask DisposeAsync() => _json.DisposeAsync();
}

/// <summary>Portable SQL: data only, one INSERT per row, inside a transaction. Run it on a database that already has the tables.</summary>
internal sealed class SqlSink(Stream output) : IBackupSink
{
    private readonly StreamWriter _writer = BackupSinks.Utf8Writer(output);
    private readonly StringBuilder _line = new();
    private string _insert = string.Empty;

    public async ValueTask BeginAsync(DateTimeOffset at)
    {
        await _writer.WriteLineAsync($"-- Mostawsef backup (SQL INSERT statements), exported {at:yyyy-MM-dd HH:mm:ss zzz}");
        await _writer.WriteLineAsync("-- Data only: run it on a database that already has these tables.");
        await _writer.WriteLineAsync("BEGIN;");
    }

    public async ValueTask BeginTableAsync(TableInfo table)
    {
        await _writer.WriteLineAsync();
        await _writer.WriteLineAsync($"-- Table {table.Name}");
        _insert = $"INSERT INTO {Sql.Quote(table.Name)} ({table.QuotedColumns}) VALUES (";
    }

    public async ValueTask WriteRowAsync(Cell[] row)
    {
        _line.Clear().Append(_insert);
        for (var i = 0; i < row.Length; i++)
        {
            if (i > 0)
            {
                _line.Append(", ");
            }

            var cell = row[i];
            switch (cell.Kind)
            {
                case CellKind.Null:
                    _line.Append("NULL");
                    break;
                case CellKind.Bool:
                    _line.Append(cell.Text == "true" ? "TRUE" : "FALSE");
                    break;
                case CellKind.Number:
                    _line.Append(cell.Text);
                    break;
                default:
                    _line.Append('\'').Append(cell.Text!.Replace("'", "''", StringComparison.Ordinal)).Append('\'');
                    break;
            }
        }

        _line.Append(");");
        await _writer.WriteLineAsync(_line.ToString());
    }

    public ValueTask EndTableAsync() => ValueTask.CompletedTask;

    public async ValueTask EndAsync()
    {
        await _writer.WriteLineAsync();
        await _writer.WriteLineAsync("COMMIT;");
        await _writer.FlushAsync();
    }

    public ValueTask DisposeAsync() => _writer.DisposeAsync();
}

/// <summary>
/// A script for PostgreSQL's psql (<c>psql -d database -f file.sql</c>): CREATE TABLE IF NOT EXISTS with the primary key,
/// then the rows with COPY, the fast bulk-load format PostgreSQL's own dumps use.
/// </summary>
internal sealed class PostgreSqlSink(Stream output) : IBackupSink
{
    private readonly StreamWriter _writer = BackupSinks.Utf8Writer(output);
    private readonly StringBuilder _line = new();

    public async ValueTask BeginAsync(DateTimeOffset at)
    {
        await _writer.WriteLineAsync($"-- Mostawsef backup (PostgreSQL), exported {at:yyyy-MM-dd HH:mm:ss zzz}");
        await _writer.WriteLineAsync("-- Restore with: psql -d <database> -f <this file>");
        await _writer.WriteLineAsync("SET client_encoding = 'UTF8';");
        await _writer.WriteLineAsync("BEGIN;");
    }

    public async ValueTask BeginTableAsync(TableInfo table)
    {
        await _writer.WriteLineAsync();
        await _writer.WriteLineAsync($"-- Table {table.Name}");

        var definitions = table.Columns
            .Select(c => $"    {Sql.Quote(c.Name)} {c.StoreType}{(c.IsNullable ? string.Empty : " NOT NULL")}")
            .ToList();
        if (table.PrimaryKey.Count > 0)
        {
            definitions.Add($"    PRIMARY KEY ({string.Join(", ", table.PrimaryKey.Select(Sql.Quote))})");
        }

        await _writer.WriteLineAsync($"CREATE TABLE IF NOT EXISTS {Sql.Quote(table.Name)} (");
        await _writer.WriteLineAsync(string.Join(",\n", definitions));
        await _writer.WriteLineAsync(");");
        await _writer.WriteLineAsync($"COPY {Sql.Quote(table.Name)} ({table.QuotedColumns}) FROM stdin;");
    }

    public async ValueTask WriteRowAsync(Cell[] row)
    {
        _line.Clear();
        for (var i = 0; i < row.Length; i++)
        {
            if (i > 0)
            {
                _line.Append('\t');
            }

            if (row[i].Kind == CellKind.Null)
            {
                _line.Append("\\N");
                continue;
            }

            foreach (var ch in row[i].Text!)
            {
                switch (ch)
                {
                    case '\\':
                        _line.Append("\\\\");
                        break;
                    case '\t':
                        _line.Append("\\t");
                        break;
                    case '\n':
                        _line.Append("\\n");
                        break;
                    case '\r':
                        _line.Append("\\r");
                        break;
                    default:
                        _line.Append(ch);
                        break;
                }
            }
        }

        await _writer.WriteLineAsync(_line.ToString());
    }

    public async ValueTask EndTableAsync() => await _writer.WriteLineAsync("\\.");

    public async ValueTask EndAsync()
    {
        await _writer.WriteLineAsync();
        await _writer.WriteLineAsync("COMMIT;");
        await _writer.FlushAsync();
    }

    public ValueTask DisposeAsync() => _writer.DisposeAsync();
}
