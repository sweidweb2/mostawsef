using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Infrastructure.Services.Backup;

/// <summary>A column as it is in the database (names and types come from the EF model, so they match the migrations).</summary>
internal sealed record ColumnInfo(string Name, string StoreType, bool IsNullable)
{
    public bool IsDate => StoreType == "date";
}

/// <summary>A table with its columns, primary key and the tables its foreign keys point to.</summary>
internal sealed record TableInfo(
    string Name,
    IReadOnlyList<ColumnInfo> Columns,
    IReadOnlyList<string> PrimaryKey,
    IReadOnlyList<string> DependsOn)
{
    /// <summary>Every column, quoted, for a SELECT or a column list.</summary>
    public string QuotedColumns => string.Join(", ", Columns.Select(c => Sql.Quote(c.Name)));

    /// <summary>
    /// Reads every table of the EF model, parents before the tables that reference them (so INSERTs satisfy foreign keys).
    /// User accounts are left out: their password hashes never leave the server, and a users table without its required
    /// hash column could not be restored anyway. Nothing references users by foreign key, so every other table restores.
    /// </summary>
    public static IReadOnlyList<TableInfo> ReadModel(IModel model)
    {
        var tables = new List<TableInfo>();
        foreach (var type in model.GetEntityTypes())
        {
            if (type.IsOwned() || type.ClrType == typeof(User) || type.GetTableName() is not { } name)
            {
                continue;
            }

            var store = StoreObjectIdentifier.Table(name, type.GetSchema());
            var columns = type.GetProperties()
                .Where(p => p.GetColumnName(store) is not null)
                .Select(p => new ColumnInfo(p.GetColumnName(store)!, p.GetColumnType(store), p.IsNullable))
                .ToList();
            var key = type.FindPrimaryKey()?.Properties.Select(p => p.GetColumnName(store)!).ToList() ?? [];
            var parents = type.GetForeignKeys()
                .Select(f => f.PrincipalEntityType.GetTableName())
                .OfType<string>()
                .Where(parent => parent != name)
                .Distinct()
                .ToList();

            tables.Add(new TableInfo(name, columns, key, parents));
        }

        return ParentsFirst(tables);
    }

    private static List<TableInfo> ParentsFirst(List<TableInfo> tables)
    {
        var remaining = tables.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
        var ordered = new List<TableInfo>(remaining.Count);
        var done = new HashSet<string>(StringComparer.Ordinal);
        var names = remaining.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        while (remaining.Count > 0)
        {
            var next = remaining.FirstOrDefault(t => t.DependsOn.All(d => done.Contains(d) || !names.Contains(d)))
                ?? remaining[0]; // a reference cycle: no order can satisfy it, keep going
            remaining.Remove(next);
            ordered.Add(next);
            done.Add(next.Name);
        }

        return ordered;
    }
}

internal enum CellKind
{
    Null,
    Bool,
    Number,
    Text,
}

/// <summary>One value, already turned into text the way every export format needs it.</summary>
internal readonly record struct Cell(CellKind Kind, string? Text)
{
    public static readonly Cell Null = new(CellKind.Null, null);

    public static Cell From(object? value, ColumnInfo column) => value switch
    {
        null or DBNull => Null,
        bool b => new Cell(CellKind.Bool, b ? "true" : "false"),
        string s => new Cell(CellKind.Text, s),
        Guid g => new Cell(CellKind.Text, g.ToString("D")),
        DateTime dt => new Cell(CellKind.Text, column.IsDate ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : Moment(dt)),
        DateTimeOffset dto => new Cell(CellKind.Text, Moment(dto.UtcDateTime)),
        DateOnly d => new Cell(CellKind.Text, d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        TimeOnly t => new Cell(CellKind.Text, t.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture)),
        byte[] bytes => new Cell(CellKind.Text, "\\x" + Convert.ToHexString(bytes).ToLowerInvariant()),
        float f => Float(f.ToString("R", CultureInfo.InvariantCulture), float.IsFinite(f)),
        double d => Float(d.ToString("R", CultureInfo.InvariantCulture), double.IsFinite(d)),
        sbyte or byte or short or ushort or int or uint or long or ulong or decimal =>
            new Cell(CellKind.Number, ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture)),
        _ => new Cell(CellKind.Text, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty),
    };

    private static Cell Float(string text, bool isFinite) => new(isFinite ? CellKind.Number : CellKind.Text, text);

    /// <summary>ISO 8601; a UTC value ends with Z so PostgreSQL reads it as the same moment.</summary>
    private static string Moment(DateTime value) => value.ToString(
        value.Kind == DateTimeKind.Utc ? "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'" : "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
        CultureInfo.InvariantCulture);
}

internal static class Sql
{
    /// <summary>"name" with embedded quotes doubled.</summary>
    public static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
