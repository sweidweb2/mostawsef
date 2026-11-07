using System;
using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>Activity logs and system errors (Super only).</summary>
public class LogsApi(ApiClient api)
{
    public Task<ApiResult<PagedResult<ActivityLogListItemDto>>> GetActivityLogsAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default) =>
        api.GetAsync<PagedResult<ActivityLogListItemDto>>(ListUrl("api/activity-logs", page, pageSize, search), cancellationToken);

    public Task<ApiResult<ActivityLogStatsDto>> GetActivityStatsAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<ActivityLogStatsDto>("api/activity-logs/stats", cancellationToken);

    public Task<ApiResult<ActivityLogDto>> GetActivityLogAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetAsync<ActivityLogDto>($"api/activity-logs/{id}", cancellationToken);

    public Task<ApiResult<bool>> DeleteActivityLogAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.DeleteAsync($"api/activity-logs/{id}", cancellationToken);

    public Task<ApiResult<PagedResult<SystemErrorListItemDto>>> GetSystemErrorsAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default) =>
        api.GetAsync<PagedResult<SystemErrorListItemDto>>(ListUrl("api/system-errors", page, pageSize, search), cancellationToken);

    public Task<ApiResult<SystemErrorStatsDto>> GetErrorStatsAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<SystemErrorStatsDto>("api/system-errors/stats", cancellationToken);

    public Task<ApiResult<SystemErrorDto>> GetSystemErrorAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetAsync<SystemErrorDto>($"api/system-errors/{id}", cancellationToken);

    public Task<ApiResult<bool>> DeleteSystemErrorAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.DeleteAsync($"api/system-errors/{id}", cancellationToken);

    public Task<ApiResult<bool>> ReportErrorAsync(ReportClientErrorRequest request, CancellationToken cancellationToken = default) =>
        api.PostAsync("api/system-errors/report", request, cancellationToken);

    private static string ListUrl(string path, int page, int pageSize, string? search) =>
        $"{path}?page={page}&pageSize={pageSize}" +
        (string.IsNullOrWhiteSpace(search) ? string.Empty : $"&search={Uri.EscapeDataString(search.Trim())}");
}
