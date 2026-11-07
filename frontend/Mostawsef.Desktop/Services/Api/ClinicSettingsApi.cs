using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Settings;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>Clinic details: everyone reads them, Admin updates them.</summary>
public class ClinicSettingsApi(ApiClient api)
{
    public Task<ApiResult<ClinicSettingsDto>> GetAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<ClinicSettingsDto>("api/clinic-settings", cancellationToken);

    public Task<ApiResult<ClinicSettingsDto>> UpdateAsync(UpdateClinicSettingsRequest request, CancellationToken cancellationToken = default) =>
        api.PutAsync<UpdateClinicSettingsRequest, ClinicSettingsDto>("api/clinic-settings", request, cancellationToken);
}
