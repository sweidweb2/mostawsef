using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mostawsef.Api.Extensions;
using Mostawsef.Application.Common;
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
    public Task<PagedResult<LabTestDto>> List(
        [FromQuery] PageQuery query,
        [FromQuery] bool? active,
        CancellationToken cancellationToken) =>
        tests.GetPageAsync(query, active, cancellationToken);

    /// <summary>Every test for print/export; pass <c>active=true</c> for the New Order catalog only.</summary>
    [HttpGet("all")]
    public Task<IReadOnlyList<LabTestDto>> All([FromQuery] bool? active, CancellationToken cancellationToken) =>
        tests.ListAllAsync(active, cancellationToken);

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

    /// <summary>
    /// Imports Name + Price ($0.08 style) from CSV. Codes are generated. Bad and duplicate-name rows are skipped.
    /// </summary>
    [HttpPost("import")]
    // Headroom above MaxImportBytes so multipart framing does not trip before our file.Length check.
    [RequestSizeLimit(LabTestRules.MaxImportBytes + (256 * 1024))]
    [ProducesResponseType<ImportLabTestsResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ImportLabTestsResult> Import(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            // Same shape as other validation failures (problem with ErrorCodes.Required on File).
            throw AppException.Validation("File", ErrorCodes.Required);
        }

        if (file.Length > LabTestRules.MaxImportBytes)
        {
            throw AppException.Validation("File", ErrorCodes.TooLarge);
        }

        await using var stream = file.OpenReadStream();
        return await tests.ImportAsync(stream, file.FileName, cancellationToken);
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
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await tests.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
