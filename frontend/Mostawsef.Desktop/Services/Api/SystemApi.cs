using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Maintenance;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>Whole-system dashboard figures and the data reset: Super only.</summary>
public class SystemApi(ApiClient api)
{
    public Task<ApiResult<SuperDashboardDto>> GetDashboardAsync(int days, CancellationToken cancellationToken = default) =>
        api.GetAsync<SuperDashboardDto>($"api/system/dashboard?days={days}", cancellationToken);

    public Task<ApiResult<ResetDataResultDto>> ResetDataAsync(string confirmation, CancellationToken cancellationToken = default) =>
        api.PostAsync<ResetDataRequest, ResetDataResultDto>(
            "api/system/reset-data", new ResetDataRequest { Confirmation = confirmation }, ApiClient.LongRequestTimeout, cancellationToken);
}
