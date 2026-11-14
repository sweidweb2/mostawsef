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

    /// <summary>Deletes the selected records (at most <see cref="LogRules.MaxBulkDelete"/> per request).</summary>
    [HttpPost("bulk-delete")]
    [ProducesResponseType<DeleteLogsResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<DeleteLogsResultDto> DeleteMany(DeleteLogsRequest request, CancellationToken cancellationToken) =>
        logs.DeleteManyAsync(request, cancellationToken);

    /// <summary>Deletes every record (the clearing is then logged as a new activity).</summary>
    [HttpDelete]
    public Task<DeleteLogsResultDto> DeleteAll(CancellationToken cancellationToken) => logs.DeleteAllAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ActivityLogDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        await logs.GetAsync(id, cancellationToken) is { } log ? Ok(log) : NotFound();
}
