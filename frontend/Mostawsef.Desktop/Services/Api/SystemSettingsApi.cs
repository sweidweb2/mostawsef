using System.Threading;
using System.Threading.Tasks;
using Mostawsef.Contracts.Settings;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>Backup folder and maintenance mode: Super only.</summary>
public class SystemSettingsApi(ApiClient api)
{
    public Task<ApiResult<SystemSettingsDto>> GetAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<SystemSettingsDto>("api/system-settings", cancellationToken);

    public Task<ApiResult<SystemSettingsDto>> UpdateAsync(UpdateSystemSettingsRequest request, CancellationToken cancellationToken = default) =>
        api.PutAsync<UpdateSystemSettingsRequest, SystemSettingsDto>("api/system-settings", request, cancellationToken);
}
