using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mostawsef.Api.Extensions;
using Mostawsef.Application.Common;
using Mostawsef.Application.Modules.Logs;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;

namespace Mostawsef.Api.Controllers;

[ApiController]
[Route("api/activity-logs")]
[Authorize(Policy = AuthPolicies.SuperOnly)]
public class ActivityLogsController(IActivityLogService logs) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CursorPage<ActivityLogListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] CursorQuery query, CancellationToken cancellationToken) =>
        PageRequest.TryCreate(query, out var request)
            ? Ok(await logs.GetPageAsync(request, cancellationToken))
            : ValidationProblem(PageRequest.InvalidMessage);

    [HttpGet("stats")]
    public Task<ActivityLogStatsDto> Stats(CancellationToken cancellationToken) => logs.GetStatsAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ActivityLogDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        await logs.GetAsync(id, cancellationToken) is { } log ? Ok(log) : NotFound();

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await logs.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
}
