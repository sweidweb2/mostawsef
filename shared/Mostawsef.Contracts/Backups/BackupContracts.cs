using System.ComponentModel.DataAnnotations;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Contracts.Backups;

public static class BackupRules
{
    public const int TableMaxLength = 100;
}

/// <summary>The formats a backup can be exported in. Sent and stored as these names.</summary>
public static class BackupFormats
{
    /// <summary>One CSV file per table (a ZIP of CSV files when all tables are exported).</summary>
    public const string Csv = "Csv";

    public const string Json = "Json";

    /// <summary>Portable SQL: one INSERT statement per row.</summary>
    public const string Sql = "Sql";

    /// <summary>A script PostgreSQL restores with psql: CREATE TABLE plus COPY data.</summary>
    public const string PostgreSql = "PostgreSql";

    public static readonly IReadOnlyList<string> All = [Csv, Json, Sql, PostgreSql];

    /// <summary>"mostawsef-patients-20261114-153000.csv"; "all" instead of the table name for every table.</summary>
    public static string FileName(string? table, string format, DateTime at) =>
        $"mostawsef-{(string.IsNullOrEmpty(table) ? "all" : table)}-{at:yyyyMMdd-HHmmss}.{Extension(table, format)}";

    public static string Extension(string? table, string format) => format switch
    {
        Csv => string.IsNullOrEmpty(table) ? "zip" : "csv",
        Json => "json",
        PostgreSql => "pg.sql",
        _ => "sql",
    };
}

/// <summary>One saved backup setting: what was exported (a table, or all tables) and in which format. The data itself is never stored.</summary>
public record BackupRecordDto(Guid Id, string? Table, string Format, DateTime CreatedAt, string? CreatedBy);

public record CreateBackupRequest
{
    /// <summary>The table to export; empty or missing means every table.</summary>
    [MaxLength(BackupRules.TableMaxLength, ErrorMessage = ErrorCodes.MaxLength)]
    public string? Table { get; init; }

    [Required(ErrorMessage = ErrorCodes.Required)]
    [AllowedValues(BackupFormats.Csv, BackupFormats.Json, BackupFormats.Sql, BackupFormats.PostgreSql, ErrorMessage = ErrorCodes.Invalid)]
    public string Format { get; init; } = string.Empty;
}
