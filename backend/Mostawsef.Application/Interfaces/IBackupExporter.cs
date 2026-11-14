using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Interfaces;

/// <summary>Reads the database and writes it out in a backup format.</summary>
public interface IBackupExporter
{
    /// <summary>Every table that can be exported, in name order.</summary>
    IReadOnlyList<string> Tables { get; }

    /// <summary>
    /// Builds the whole export first (from one consistent snapshot of the data) and returns it positioned at the start,
    /// so a failure is reported as an error instead of ending a download halfway. The caller disposes the stream.
    /// <paramref name="table"/> null means every table.
    /// </summary>
    Task<Stream> ExportAsync(string? table, BackupFormat format, CancellationToken cancellationToken);
}
