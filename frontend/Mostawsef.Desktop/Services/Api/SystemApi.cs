using System;
using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Maintenance;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>Whole-system dashboard figures and the data reset: Super only.</summary>
public class SystemApi(ApiClient api)
{
    /// <summary>The Super dashboard for the days <paramref name="from"/> to <paramref name="to"/> (both included).</summary>
    public Task<ApiResult<SuperDashboardDto>> GetDashboardAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) =>
        api.GetAsync<SuperDashboardDto>($"api/system/dashboard?{DashboardQuery.Of(from, to)}", cancellationToken);

    public Task<ApiResult<ResetDataResultDto>> ResetDataAsync(string confirmation, CancellationToken cancellationToken = default) =>
        api.PostAsync<ResetDataRequest, ResetDataResultDto>(
            "api/system/reset-data", new ResetDataRequest { Confirmation = confirmation }, ApiClient.LongRequestTimeout, cancellationToken);
}
