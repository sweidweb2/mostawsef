using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mostawsef.Api.Extensions;
using Mostawsef.Application.Modules.Settings;
using Mostawsef.Contracts.Settings;

namespace Mostawsef.Api.Controllers;

/// <summary>Backup folder and maintenance mode: Super only.</summary>
[ApiController]
[Route("api/system-settings")]
[Authorize(Policy = AuthPolicies.SuperOnly)]
public class SystemSettingsController(ISystemSettingsService settings) : ControllerBase
{
    [HttpGet]
    public Task<SystemSettingsDto> Get(CancellationToken cancellationToken) => settings.GetAsync(cancellationToken);

    [HttpPut]
    [ProducesResponseType<SystemSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<SystemSettingsDto> Update(UpdateSystemSettingsRequest request, CancellationToken cancellationToken) =>
        settings.UpdateAsync(request, cancellationToken);
}
