using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mostawsef.Api.Extensions;
using Mostawsef.Application.Modules.Backups;
using Mostawsef.Contracts.Backups;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Api.Controllers;

/// <summary>History of scheduled database dumps (Super only). Files stay on disk; this lists when/where/settings.</summary>
[ApiController]
[Route("api/scheduled-dumps")]
[Authorize(Policy = AuthPolicies.SuperOnly)]
public class ScheduledDumpsController(IScheduledBackupService backups) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ScheduledDumpDto>> List([FromQuery] PageQuery query, CancellationToken cancellationToken) =>
        backups.GetHistoryAsync(query, cancellationToken);
}
