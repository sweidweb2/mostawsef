using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Mostawsef.Api.Extensions;
using Mostawsef.Application.Common;
using Mostawsef.Application.Modules.Logs;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Logs;

namespace Mostawsef.Api.Controllers;

[ApiController]
[Route("api/system-errors")]
[Authorize(Policy = AuthPolicies.SuperOnly)]
public class SystemErrorsController(ISystemErrorService errors) : ControllerBase
{
    /// <summary>Largest accepted body of an (anonymous) client error report; the stored fields are truncated further.</summary>
    private const int MaxReportBytes = 128 * 1024;

    [HttpGet]
    [ProducesResponseType<CursorPage<SystemErrorListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] CursorQuery query, CancellationToken cancellationToken) =>
        PageRequest.TryCreate(query, out var request)
            ? Ok(await errors.GetPageAsync(request, cancellationToken))
            : ValidationProblem(PageRequest.InvalidMessage);

    [HttpGet("stats")]
    public Task<SystemErrorStatsDto> Stats(CancellationToken cancellationToken) => errors.GetStatsAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<SystemErrorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        await errors.GetAsync(id, cancellationToken) is { } error ? Ok(error) : NotFound();

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await errors.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>The desktop app reports its unexpected errors here, also before sign-in (rate limited).</summary>
    [HttpPost("report")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthPolicies.ClientErrorRateLimit)]
    [RequestSizeLimit(MaxReportBytes)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Report(ReportClientErrorRequest request, CancellationToken cancellationToken)
    {
        await errors.ReportClientErrorAsync(request, cancellationToken);
        return Accepted();
    }
}
