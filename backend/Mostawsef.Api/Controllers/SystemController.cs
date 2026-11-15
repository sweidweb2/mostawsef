using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mostawsef.Api.Extensions;
using Mostawsef.Application.Modules.Maintenance;
using Mostawsef.Contracts.Dashboards;
using Mostawsef.Contracts.Maintenance;

namespace Mostawsef.Api.Controllers;

/// <summary>Whole-system figures and the data reset: Super only.</summary>
[ApiController]
[Route("api/system")]
[Authorize(Policy = AuthPolicies.SuperOnly)]
public class SystemController(ISystemMaintenanceService maintenance) : ControllerBase
{
    /// <summary>The Super dashboard for a period (days in the server's time zone, kept between the first record and today).</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType<SuperDashboardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<SuperDashboardDto> Dashboard([FromQuery] DashboardRangeQuery query, CancellationToken cancellationToken) =>
        maintenance.GetDashboardAsync(query, cancellationToken);

    /// <summary>
    /// Deletes every patient, order, payment, lab test, activity log and system error. User accounts, the clinic and
    /// system settings and the backup history stay. The request must carry the confirmation word.
    /// </summary>
    /// <remarks>Not tied to the request: once confirmed it runs to the end even if the client leaves or disconnects.</remarks>
    [HttpPost("reset-data")]
    [ProducesResponseType<ResetDataResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<ResetDataResultDto> ResetData(ResetDataRequest request) =>
        maintenance.ResetDataAsync(request, CancellationToken.None);
}
