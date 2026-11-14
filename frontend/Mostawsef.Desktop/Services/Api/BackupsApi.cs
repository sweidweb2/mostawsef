using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>Database backups (Super only).</summary>
public class BackupsApi(ApiClient api)
{
    public Task<ApiResult<IReadOnlyList<string>>> GetTablesAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<string>>("api/backups/tables", cancellationToken);

    public Task<ApiResult<PagedResult<BackupRecordDto>>> GetBackupsAsync(
        int page, int pageSize, bool includeTotal = true, CancellationToken cancellationToken = default) =>
        api.GetAsync<PagedResult<BackupRecordDto>>(
            $"api/backups?page={page}&pageSize={pageSize}&includeTotal={includeTotal.ToString().ToLowerInvariant()}",
            cancellationToken);

    /// <summary>Saves the settings of a backup (a new history row). Nothing is exported yet.</summary>
    public Task<ApiResult<BackupRecordDto>> CreateAsync(CreateBackupRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync<CreateBackupRequest, BackupRecordDto>("api/backups", request, cancellationToken);

    /// <summary>Exports the current data with the settings of a history row into <paramref name="destinationPath"/>.</summary>
    public Task<ApiResult<bool>> ExportAsync(Guid id, string destinationPath, CancellationToken cancellationToken = default) =>
        api.DownloadAsync($"api/backups/{id}/export", destinationPath, cancellationToken);
}
