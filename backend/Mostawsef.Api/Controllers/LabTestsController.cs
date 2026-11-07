using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mostawsef.Api.Extensions;
using Mostawsef.Application.Modules.LabTests;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.LabTests;

namespace Mostawsef.Api.Controllers;

/// <summary>The clinic's lab test catalog. Failures come back as problem responses with an ErrorCodes "code".</summary>
[ApiController]
[Route("api/lab-tests")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public class LabTestsController(ILabTestService tests) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<LabTestDto>> List([FromQuery] PageQuery query, CancellationToken cancellationToken) =>
        tests.GetPageAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<LabTestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<LabTestDto> Get(Guid id, CancellationToken cancellationToken) => tests.GetAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType<LabTestDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateLabTestRequest request, CancellationToken cancellationToken)
    {
        var test = await tests.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = test.Id }, test);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<LabTestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<LabTestDto> Update(Guid id, UpdateLabTestRequest request, CancellationToken cancellationToken) =>
        tests.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await tests.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
