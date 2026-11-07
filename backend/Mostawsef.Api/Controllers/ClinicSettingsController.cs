using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mostawsef.Api.Extensions;
using Mostawsef.Application.Modules.Settings;
using Mostawsef.Contracts.Settings;

namespace Mostawsef.Api.Controllers;

/// <summary>Clinic details: every signed-in user reads them (headers, printouts, currency); Admin edits them.</summary>
[ApiController]
[Route("api/clinic-settings")]
[Authorize]
public class ClinicSettingsController(IClinicSettingsService settings) : ControllerBase
{
    [HttpGet]
    public Task<ClinicSettingsDto> Get(CancellationToken cancellationToken) => settings.GetAsync(cancellationToken);

    [HttpPut]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType<ClinicSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ClinicSettingsDto> Update(UpdateClinicSettingsRequest request, CancellationToken cancellationToken) =>
        settings.UpdateAsync(request, cancellationToken);
}
