using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mostawsef.Api.Extensions;
using Mostawsef.Application.Modules.Maintenance;
using Mostawsef.Contracts.Maintenance;

namespace Mostawsef.Api.Controllers;

/// <summary>Whole-system figures and the data reset: Super only.</summary>
[ApiController]
[Route("api/system")]
[Authorize(Policy = AuthPolicies.SuperOnly)]
public class SystemController(ISystemMaintenanceService maintenance) : ControllerBase
{
    /// <summary>Totals, orders per status and per-day series (default the last 30 days) for the Super dashboard.</summary>
    [HttpGet("dashboard")]
    public Task<SuperDashboardDto> Dashboard([FromQuery] int days = 30, CancellationToken cancellationToken = default) =>
        maintenance.GetDashboardAsync(days, cancellationToken);

    /// <summary>
    /// Deletes every patient, order, payment, lab test, activity log and system error. User accounts, the clinic and
    /// system settings and the backup history stay. The request must carry the confirmation word.
    /// </summary>
    [HttpPost("reset-data")]
    [ProducesResponseType<ResetDataResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ResetDataResultDto> ResetData(ResetDataRequest request, CancellationToken cancellationToken) =>
        maintenance.ResetDataAsync(request, cancellationToken);
}
