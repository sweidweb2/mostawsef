using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>Scheduled database dump history (Super only).</summary>
public class ScheduledDumpsApi(ApiClient api)
{
    public Task<ApiResult<PagedResult<ScheduledDumpDto>>> GetPageAsync(
        int page, int pageSize, bool includeTotal = true, CancellationToken cancellationToken = default) =>
        api.GetAsync<PagedResult<ScheduledDumpDto>>(
            $"api/scheduled-dumps?page={page}&pageSize={pageSize}&includeTotal={includeTotal.ToString().ToLowerInvariant()}",
            cancellationToken);
}
