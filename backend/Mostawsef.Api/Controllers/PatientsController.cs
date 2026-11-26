using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mostawsef.Api.Extensions;
using Mostawsef.Application.Modules.Patients;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Patients;

namespace Mostawsef.Api.Controllers;

/// <summary>Patient records. Failures come back as problem responses with an ErrorCodes "code".</summary>
[ApiController]
[Route("api/patients")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public class PatientsController(IPatientService patients) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PatientDto>> List(
        [FromQuery] PageQuery query, [FromQuery] bool includeUnpaid, CancellationToken cancellationToken) =>
        patients.GetPageAsync(query, includeUnpaid, cancellationToken);

    /// <summary>Every patient for print/export (no notes).</summary>
    [HttpGet("all")]
    public Task<IReadOnlyList<PatientDto>> All(CancellationToken cancellationToken) =>
        patients.ListAllAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PatientDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<PatientDto> Get(Guid id, CancellationToken cancellationToken) => patients.GetAsync(id, cancellationToken);

    /// <summary>Full patient (with notes) and newest orders for the details page.</summary>
    [HttpGet("{id:guid}/details")]
    [ProducesResponseType<PatientDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<PatientDetailsDto> Details(Guid id, CancellationToken cancellationToken) =>
        patients.GetDetailsAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType<PatientDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreatePatientRequest request, CancellationToken cancellationToken)
    {
        var patient = await patients.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = patient.Id }, patient);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<PatientDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<PatientDto> Update(Guid id, UpdatePatientRequest request, CancellationToken cancellationToken) =>
        patients.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await patients.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
