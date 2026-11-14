using Mostawsef.Domain.Common;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Domain.Entities;

/// <summary>
/// The settings of one backup export (which table, which format). Only the settings are stored, never the exported data:
/// exporting a record again rebuilds the file from the current database.
/// </summary>
public class BackupRecord : Entity
{
    public const int TableMaxLength = 100;

    private BackupRecord()
    {
    }

    public BackupRecord(string? table, BackupFormat format)
    {
        Table = string.IsNullOrWhiteSpace(table) ? null : table.Trim();
        Format = format;
    }

    /// <summary>The exported table; null means every table.</summary>
    public string? Table { get; private set; }

    public BackupFormat Format { get; private set; }
}
