using Mostawsef.Application.Common;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Application.Modules.Backups;

/// <summary>An export ready to download. The caller disposes <see cref="Content"/>.</summary>
public sealed record BackupFile(string FileName, Stream Content);

/// <summary>Backups (Super only): saving what to export, listing the history, exporting again with the saved settings. Failures throw AppException.</summary>
public interface IBackupService
{
    IReadOnlyList<string> GetTables();

    Task<PagedResult<BackupRecordDto>> GetPageAsync(PageQuery query, CancellationToken cancellationToken);

    /// <summary>Saves the settings as a new history row. The export itself is a separate call.</summary>
    Task<BackupRecordDto> CreateAsync(CreateBackupRequest request, CancellationToken cancellationToken);

    /// <summary>Exports the current data with the settings saved in the history row.</summary>
    Task<BackupFile> ExportAsync(Guid id, CancellationToken cancellationToken);
}

public class BackupService(
    IBackupRepository backups,
    IBackupExporter exporter,
    IActivityLogger activity,
    TimeProvider time) : IBackupService
{
    public IReadOnlyList<string> GetTables() => exporter.Tables;

    public async Task<PagedResult<BackupRecordDto>> GetPageAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var (items, total) = await backups.GetPageAsync(query, cancellationToken);
        return new PagedResult<BackupRecordDto>(items, query.SafePage, query.SafePageSize, total);
    }

    public async Task<BackupRecordDto> CreateAsync(CreateBackupRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<BackupFormat>(request.Format, ignoreCase: false, out var format)
            || !Enum.IsDefined(format))
        {
            throw AppException.Validation(nameof(CreateBackupRequest.Format), ErrorCodes.Invalid);
        }

        var table = string.IsNullOrWhiteSpace(request.Table) ? null : request.Table.Trim();
        if (table is not null && !exporter.Tables.Contains(table, StringComparer.Ordinal))
        {
            throw AppException.Validation(nameof(CreateBackupRequest.Table), ErrorCodes.Invalid);
        }

        var record = new BackupRecord(table, format);
        backups.Add(record);
        await backups.SaveChangesAsync(cancellationToken);

        await activity.LogAsync(
            new ActivityEntry(ActivityActions.BackupCreated, ActivityModules.Backups, "Saved a backup setting")
            {
                EntityType = nameof(BackupRecord),
                EntityId = record.Id.ToString(),
                Details = new { Table = table ?? "all", Format = format.ToString() },
            },
            cancellationToken);

        return new BackupRecordDto(record.Id, record.Table, record.Format.ToString(), record.CreatedAt, record.CreatedBy);
    }

    public async Task<BackupFile> ExportAsync(Guid id, CancellationToken cancellationToken)
    {
        var record = await backups.GetAsync(id, cancellationToken) ?? throw AppException.NotFound();
        var format = Enum.Parse<BackupFormat>(record.Format);

        // A table that no longer exists (renamed in a later version) cannot be exported any more.
        if (record.Table is not null && !exporter.Tables.Contains(record.Table, StringComparer.Ordinal))
        {
            throw AppException.NotFound();
        }

        var content = await exporter.ExportAsync(record.Table, format, cancellationToken);
        try
        {
            await activity.LogAsync(
                new ActivityEntry(ActivityActions.BackupExported, ActivityModules.Backups, "Exported a backup")
                {
                    EntityType = nameof(BackupRecord),
                    EntityId = record.Id.ToString(),
                    Details = new { Table = record.Table ?? "all", record.Format },
                },
                cancellationToken);
        }
        catch
        {
            await content.DisposeAsync();
            throw;
        }

        return new BackupFile(BackupFormats.FileName(record.Table, record.Format, time.GetLocalNow().DateTime), content);
    }
}
