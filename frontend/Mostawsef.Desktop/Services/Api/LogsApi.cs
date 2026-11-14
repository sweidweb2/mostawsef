using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>Activity logs and system errors (Super only).</summary>
public class LogsApi(ApiClient api)
{
    public Task<ApiResult<CursorPage<ActivityLogListItemDto>>> GetActivityLogsAsync(CursorQuery query, CancellationToken cancellationToken = default) =>
        api.GetAsync<CursorPage<ActivityLogListItemDto>>(ListUrl("api/activity-logs", query), cancellationToken);

    public Task<ApiResult<ActivityLogStatsDto>> GetActivityStatsAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<ActivityLogStatsDto>("api/activity-logs/stats", cancellationToken);

    public Task<ApiResult<ActivityLogDto>> GetActivityLogAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetAsync<ActivityLogDto>($"api/activity-logs/{id}", cancellationToken);

    public Task<ApiResult<CursorPage<SystemErrorListItemDto>>> GetSystemErrorsAsync(CursorQuery query, CancellationToken cancellationToken = default) =>
        api.GetAsync<CursorPage<SystemErrorListItemDto>>(ListUrl("api/system-errors", query), cancellationToken);

    public Task<ApiResult<SystemErrorStatsDto>> GetErrorStatsAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<SystemErrorStatsDto>("api/system-errors/stats", cancellationToken);

    public Task<ApiResult<SystemErrorDto>> GetSystemErrorAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetAsync<SystemErrorDto>($"api/system-errors/{id}", cancellationToken);

    public Task<ApiResult<bool>> DeleteSystemErrorAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.DeleteAsync($"api/system-errors/{id}", cancellationToken);

    public Task<ApiResult<DeleteLogsResultDto>> DeleteActivityLogsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default) =>
        api.PostAsync<DeleteLogsRequest, DeleteLogsResultDto>("api/activity-logs/bulk-delete", new DeleteLogsRequest { Ids = ids }, cancellationToken);

    public Task<ApiResult<bool>> DeleteAllActivityLogsAsync(CancellationToken cancellationToken = default) =>
        api.DeleteAsync("api/activity-logs", cancellationToken);

    public Task<ApiResult<DeleteLogsResultDto>> DeleteSystemErrorsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default) =>
        api.PostAsync<DeleteLogsRequest, DeleteLogsResultDto>("api/system-errors/bulk-delete", new DeleteLogsRequest { Ids = ids }, cancellationToken);

    public Task<ApiResult<bool>> DeleteAllSystemErrorsAsync(CancellationToken cancellationToken = default) =>
        api.DeleteAsync("api/system-errors", cancellationToken);

    public Task<ApiResult<bool>> ReportErrorAsync(ReportClientErrorRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync("api/system-errors/report", request, cancellationToken);

    private static string ListUrl(string path, CursorQuery query)
    {
        var url = new StringBuilder($"{path}?pageSize={query.PageSize}");
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            url.Append("&search=").Append(Uri.EscapeDataString(query.Search.Trim()));
        }

        if (query.After is not null)
        {
            url.Append("&after=").Append(Uri.EscapeDataString(query.After));
        }

        if (query.Before is not null)
        {
            url.Append("&before=").Append(Uri.EscapeDataString(query.Before));
        }

        if (query.Last)
        {
            url.Append("&last=true");
        }

        if (!query.IncludeTotal)
        {
            url.Append("&includeTotal=false");
        }

        return url.ToString();
    }
}
